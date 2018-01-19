﻿using CK.Glouton.Model.Server.Handlers;
using CK.Glouton.Model.Services;
using CK.Glouton.Service.Common;
using System;

namespace CK.Glouton.Service
{
    public class AlertService : IAlertService
    {
        private readonly AlertSenderManager _alertSenderManager;

        public AlertService()
        {
            _alertSenderManager = new AlertSenderManager();
        }

        public bool AddAlert( IAlertExpressionModel alertExpression )
        {
            try
            {
                var condition = alertExpression.Expressions.Build();

                return true;
            }
            catch( Exception )
            {
                return false;
            }
        }
    }
}