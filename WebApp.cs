using System;
using System.Net;
using System.Collections.Generic;
using Manager.Model;
using System.Resources;

namespace ManagerIO_Sqlite
{
	public class WebApp
	{
		WebServer ws;
		Model model;
		string prefix;
		public WebApp (Model model,string prefix)
		{
			this.model = model;
			this.prefix = prefix;
		}
		public void Start() {
			ws = new WebServer(SendResponse, prefix);
			ws.Run();
		}
		public void Stop() {
			ws.Stop();
		}



		public string ConvertToTransfer(HttpListenerContext ctx) {
			const String accountPrefix = "paymentreceipt_";
			model.BeginTransaction();
			foreach(string key in ctx.Request.QueryString) {
				if(key.StartsWith(accountPrefix)) {
					string[] guids= key.Substring(accountPrefix.Length).Split(new char[]{'_'});
					object payment=model.GetObject(Guid.Parse(guids[0]));
					object receipt=model.GetObject(Guid.Parse(guids[1]));
					model.ConvertToTransfer(new Model.PaymentReceipt{
						Payment=payment as Payment,Receipt=receipt as Receipt
					});
				}
			}
			model.CloseTransaction();
			ctx.Response.StatusCode = (int)HttpStatusCode.Redirect;
			ctx.Response.RedirectLocation = ctx.Request.QueryString.Get("PathAndQuery");
			return "";
		}
		public string SendAccountsResponse(HttpListenerContext ctx) {
			List<CashAccount2> accounts=model.GetCashAccounts();
			//			selectAccounts.Session["accounts"] = accounts;

			Dictionary<Guid,Boolean> selectedAccounts = new Dictionary<Guid,Boolean>();
			accounts.Sort((x, y) => x.Name.ToLower().CompareTo(y.Name.ToLower()));
			const String accountPrefix = "account_";
			foreach(string key in ctx.Request.QueryString) {
				if(key.StartsWith(accountPrefix)) {
					Guid accountId = Guid.Parse(key.Substring(accountPrefix.Length));
					selectedAccounts.Add(accountId,true);
				}
			}
			/*
			SelectAccounts selectAccounts=new SelectAccounts{
				Accounts=accounts,SelectedAccounts=selectedAccounts,
				QueryString=ctx.Request.QueryString
			};
			*/

			Dictionary<Guid,CashAccount2> cashAccountsHash=model.ListToDictionary<CashAccount2>(accounts);


			Decimal minCurrency = Convert.ToDecimal(ctx.Request.QueryString.Get("minCurrency"));
			Decimal maxCurrency = Convert.ToDecimal(ctx.Request.QueryString.Get("maxCurrency"));
			double maxDays = Convert.ToDouble(ctx.Request.QueryString.Get("maxDays"));
			//List<Payment> payments = model.GetPayments();
			List<Model.PaymentReceipt> paymentReceipts=model.FindTransfers(
				selectedAccounts,minCurrency,maxCurrency,maxDays);
			/*
			ListTransactions listTransactions=new ListTransactions{
				PaymentReceipts=paymentReceipts,
				CashAccounts=cashAccountsHash
			};
			*/

			SearchPage searchPage=new SearchPage{
				PaymentReceipts=paymentReceipts,
				CashAccounts=cashAccountsHash,
				Accounts=accounts,SelectedAccounts=selectedAccounts,
				QueryString=ctx.Request.QueryString,
				PathAndQuery=ctx.Request.Url.PathAndQuery
			};

			return searchPage.TransformText();
		}
		public byte[] SendResponse(HttpListenerContext ctx)
		{
			string html;
			/*
			if(ctx.Request.Url.AbsolutePath.StartsWith("/public")) {
				byte[] b;
				ResourceReader.GetResourceData(ctx.Request.Url.AbsolutePath, "Byte", b);
				return b;
			} else 
				*/
			if(ctx.Request.Url.AbsolutePath == "/ConvertToTransfer") {
				html= ConvertToTransfer(ctx);
			} else {
				html= SendAccountsResponse(ctx);
			}
			ctx.Response.ContentType="text/html; charset=utf-8;";
			byte[] buf = System.Text.Encoding.UTF8.GetBytes(html);
			ctx.Response.ContentLength64 = buf.Length;
			return buf;
		}
	}
}

