﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Silky.Core.Exceptions;
using Silky.Core.Extensions;

namespace Silky.Rpc.Runtime.Server
{
    public class RpcParameter
    {
        public RpcParameter(
            ParameterFrom @from,
            ParameterInfo parameterInfo,
            int index,
            string[] cacheKeyTemplates,
            string name = null,
            string pathTemplate = null)
        {
            From = @from;
            ParameterInfo = parameterInfo;
            Name = !name.IsNullOrEmpty() ? name : parameterInfo.Name;
            SampleName = Name.Split(":")[0];
            Type = parameterInfo.ParameterType;
            CacheKeys = CreateCacheKeys(cacheKeyTemplates);
            PathTemplate = pathTemplate;
            Index = index;
            if (@from == ParameterFrom.Path && PathTemplate.IsNullOrEmpty())
            {
                PathTemplate = "{" + Name + "}";
            }
        }

        private IReadOnlyCollection<ICacheKeyProvider> CreateCacheKeys(string[] cacheKeyTemplates)
        {
            var cacheKeys = new List<ICacheKeyProvider>();
            var attributeInfoCacheKeyProviders = ParserAttributeCacheKeyProviders();

            if (attributeInfoCacheKeyProviders != null)
            {
                cacheKeys.AddRange(attributeInfoCacheKeyProviders);
            }

            var namedCacheKeyProviders = ParserNamedCacheKeyProviders(cacheKeyTemplates);
            cacheKeys.AddRange(namedCacheKeyProviders);
            return cacheKeys;
        }

        private ICollection<ICacheKeyProvider> ParserNamedCacheKeyProviders(string[] cacheKeyTemplates)
        {
            var cacheKeys = new List<ICacheKeyProvider>();
            var cacheKeyParameters =
                cacheKeyTemplates.SelectMany(p =>
                    Regex.Matches(p, CacheKeyConstants.CacheKeyParameterRegex)
                        .Select(q => q.Value.RemoveCurlyBraces()));

            foreach (var cacheKeyParameter in cacheKeyParameters)
            {
                if (Type.IsSample() && SampleName.Equals(cacheKeyParameter, StringComparison.OrdinalIgnoreCase))
                {
                    cacheKeys.Add(new NamedCacheKeyProvider(SampleName, Index));
                    break;
                }

                var properties = Type.GetProperties();
                foreach (var property in properties)
                {
                    if (property.Name.Equals(cacheKeyParameter, StringComparison.OrdinalIgnoreCase))
                    {
                        cacheKeys.Add(new NamedCacheKeyProvider(property.Name, Index));
                        break;
                    }
                }
            }

            return cacheKeys;
        }

        private ICollection<ICacheKeyProvider> ParserAttributeCacheKeyProviders()
        {
            var cacheKeys = new List<ICacheKeyProvider>();
            var parameterInfoCacheKeyProvider =
                ParameterInfo.GetCustomAttributes().OfType<ICacheKeyProvider>().FirstOrDefault();
            if (IsSingleType && parameterInfoCacheKeyProvider != null)
            {
                parameterInfoCacheKeyProvider.PropName = SampleName;
                cacheKeys.Add(parameterInfoCacheKeyProvider);
            }
            else if (!IsSingleType && parameterInfoCacheKeyProvider != null)
            {
                throw new SilkyException("Complex parameter types are not allowed to use CacheKeyAttribute");
            }
            else
            {
                var propCacheKeyProviderInfos =
                    Type.GetProperties().Select(p => new
                        {
                            CacheKeyProvider = p.GetCustomAttributes().OfType<ICacheKeyProvider>().FirstOrDefault(),
                            PropertyInfo = p
                        })
                        .Where(p => p.CacheKeyProvider != null);
                foreach (var propCacheKeyProviderInfo in propCacheKeyProviderInfos)
                {
                    var propInfoCacheKeyProvider = propCacheKeyProviderInfo.CacheKeyProvider;
                    propInfoCacheKeyProvider.PropName = propCacheKeyProviderInfo.PropertyInfo.Name;
                    cacheKeys.Add(propInfoCacheKeyProvider);
                }
            }

            return cacheKeys;
        }
        
        public ParameterFrom From { get; }

        public Type Type { get; }
        
        public int Index { get; }

        public string Name { get; }

        public string SampleName { get; }

        public string PathTemplate { get; }

        public bool IsSingleType => Type.IsSample() || Type.IsNullableType() || Type.IsEnumerable();
        

        public bool IsNullableType => Type.IsNullableType();


        public ParameterInfo ParameterInfo { get; }

        public IReadOnlyCollection<ICacheKeyProvider> CacheKeys { get; }
    }
}