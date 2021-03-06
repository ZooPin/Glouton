﻿using System;
using CK.Glouton.Model.Server.Sender.Implementation;

namespace CK.Glouton.Model.Server.Handlers.Implementation
{
    [Serializable]
    public class AlertExpressionModel
    {
        public ExpressionModel[] Expressions { get; set; }
        public AlertSenderConfiguration[] Senders { get; set; }
    }
}
