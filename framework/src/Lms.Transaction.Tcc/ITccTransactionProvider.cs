﻿namespace Lms.Transaction.Tcc
{
    public interface ITccTransactionProvider
    { 
        string ConfirmMethod { get; set; }
        
        string CancelMethod { get; set; }
    }
}