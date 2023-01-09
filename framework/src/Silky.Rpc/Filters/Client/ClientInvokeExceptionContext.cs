﻿using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Silky.Rpc.Runtime.Client;
using Silky.Rpc.Transport.Messages;

namespace Silky.Rpc.Filters;

public class ClientInvokeExceptionContext : ClientFilterContext
{
    private Exception? _exception;
    private ExceptionDispatchInfo? _exceptionDispatchInfo;

    public ClientInvokeExceptionContext(ClientInvokeContext context, IList<IFilterMetadata> filters) : base(context, filters)
    {
    }

    public virtual Exception Exception
    {
        get
        {
            if (_exception == null && _exceptionDispatchInfo != null)
            {
                return _exceptionDispatchInfo.SourceException;
            }
            else
            {
                return _exception!;
            }
        }

        set
        {
            _exceptionDispatchInfo = null;
            _exception = value;
        }
    }

    public virtual ExceptionDispatchInfo? ExceptionDispatchInfo
    {
        get { return _exceptionDispatchInfo; }

        set
        {
            _exception = null;
            _exceptionDispatchInfo = value;
        }
    }

    public virtual bool ExceptionHandled { get; set; }

    public virtual RemoteResultMessage? Result { get; set; }
}