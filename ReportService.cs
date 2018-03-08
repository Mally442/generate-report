using System;
using System.Collections.Generic;
using BLL.Services.Domain.Billing;
using Website.Areas.Admin.Models.Billing;
using System.Dynamic;
using Newtonsoft.Json;
using BLL.Helpers.Dynamic;

public interface IReportService
{
	GenerateReportModel GenerateReport(ReportFilterModel model);
}

public class ReportService : IReportService
{
	private readonly IWalletService walletService;
	private readonly ITransactionService transactionService;

	public ReportService(IWalletService walletService, ITransactionService transactionService)
	{
		this.walletService = walletService;
		this.transactionService = transactionService;
	}

	public GenerateReportModel GenerateReport(ReportFilterModel model)
	{
		var wallet = this.walletService.Repository.Get(model.WalletId);

		model.To = model.To.HasValue ? model.To.Value.AddDays(1).AddMilliseconds(-1) : (DateTime?)null;
		var searchTransactionsResult = this.transactionService.SearchTransactions(wallet.Key, wallet.Token, null, null, model.From, model.To, 0, int.MaxValue);

		decimal rechargeTotal = 0.0M;
		decimal expireTotal = 0.0M;
		decimal usageTotal = 0.0M;
		var transactions = new List<dynamic>();
		foreach(var remoteTransaction in searchTransactionsResult.Items)
		{
			switch(remoteTransaction.Type)
			{
				case "OpeningBalance":
					break;
				case "Recharge":
					rechargeTotal += remoteTransaction.Amount;
					break;
				case "RechargeExpired":
					expireTotal -= remoteTransaction.Amount;
					break;
				default:
					usageTotal -= remoteTransaction.Amount;
					break;
			}

			dynamic transaction = JsonConvert.DeserializeObject<ExpandoObject>(remoteTransaction.Detail);
			if (transaction == null)
			{
				transaction = new ExpandoObject();
			}
			if (!ExpandoHelper.HasProperty(transaction, "RuleId") && remoteTransaction.RuleId.HasValue && remoteTransaction.RuleId.Value > 0)
			{
				transaction.RuleId = remoteTransaction.RuleId;
			}
			transaction.RuleName = remoteTransaction.RuleName;
			transaction.RuleType = remoteTransaction.RuleType;
			transaction.Date = remoteTransaction.Date.ToLocalTime();
			transaction.Amount = remoteTransaction.Amount;
			transaction.TransactionType = remoteTransaction.Type;
			transaction.Comment = remoteTransaction.Comment;
			transactions.Add(transaction);
		}

		string amountFormatString;
		switch(wallet.Type)
		{
			case "Credit":
				amountFormatString = "{0:#,##0.##} credits";
				break;
			case "SMS":
				amountFormatString = "{0:#,##0.##} credits";
				break;
			case "Airtime":
				amountFormatString = "R {0:#,##0.00}";
				break;
			case "Support":
				amountFormatString = "{0:#,##0.##} hours";
				break;
			case "DeviceLicence":
			case "MobenziIDLicence":
			case "SureLockLicence":
				amountFormatString = "{0:#,##0.} licences";
				break;
			default:
				amountFormatString = "{0:#,##0.00}";
				break;
		}

		return new GenerateReportModel(wallet, searchTransactionsResult, amountFormatString, rechargeTotal, expireTotal, usageTotal, transactions);
	}
}