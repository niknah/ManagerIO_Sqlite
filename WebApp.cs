using System;
using System.Net;
using System.Collections.Generic;
using Manager.Model;
using System.Resources;
using System.IO;
using System.Runtime.Serialization;
using Json;

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
                        Payment=payment as ReceiptOrPayment,Receipt=receipt as ReceiptOrPayment
                    });
				}
			}
			model.CloseTransaction();
			ctx.Response.StatusCode = (int)HttpStatusCode.Redirect;
			ctx.Response.RedirectLocation = ctx.Request.QueryString.Get("PathAndQuery");
			return "";
		}
		public string SendAccountsResponse(HttpListenerContext ctx) {
			List<BankAccount> accounts=model.GetBankAccounts();
			//			selectAccounts.Session["accounts"] = accounts;

			Dictionary<Guid,Boolean> selectedAccounts = GetSelectedAccounts(ctx,accounts);
			/*
			Dictionary<Guid,Boolean> selectedAccounts = new Dictionary<Guid,Boolean>();
			accounts.Sort((x, y) => x.Name.ToLower().CompareTo(y.Name.ToLower()));
			const String accountPrefix = "account_";
			foreach(string key in ctx.Request.QueryString) {
				if(key.StartsWith(accountPrefix)) {
					Guid accountId = Guid.Parse(key.Substring(accountPrefix.Length));
					selectedAccounts.Add(accountId,true);
				}
			}
			SelectAccounts selectAccounts=new SelectAccounts{
				Accounts=accounts,SelectedAccounts=selectedAccounts,
				QueryString=ctx.Request.QueryString
			};
			*/

			Dictionary<Guid,BankAccount> bankAccountsHash=model.ListToDictionary<BankAccount>(accounts);


			Decimal minCurrency = Convert.ToDecimal(ctx.Request.QueryString.Get("minCurrency"));
			Decimal maxCurrency = Convert.ToDecimal(ctx.Request.QueryString.Get("maxCurrency"));
			double maxDays = Convert.ToDouble(ctx.Request.QueryString.Get("maxDays"));
			//List<Payment> payments = model.GetPayments();
			List<Model.PaymentReceipt> paymentReceipts=model.FindTransfers(
				selectedAccounts,minCurrency,maxCurrency,maxDays);

			SearchPaymentReceiptPage searchPage=new SearchPaymentReceiptPage{
				PaymentReceipts=paymentReceipts,
				BankAccounts=bankAccountsHash,
				Accounts=accounts,SelectedAccounts=selectedAccounts,
				QueryString=ctx.Request.QueryString,
				PathAndQuery=ctx.Request.Url.PathAndQuery
			};

			return searchPage.TransformText();
		}

		private Dictionary<Guid,Boolean> GetSelectedAccounts(HttpListenerContext ctx,List<BankAccount> accounts) {
			Dictionary<Guid,Boolean> selectedAccounts = new Dictionary<Guid,Boolean>();
			accounts.Sort((x, y) => x.Name.ToLower().CompareTo(y.Name.ToLower()));
			const String accountPrefix = "account_";
			foreach(string key in ctx.Request.QueryString) {
				if(key.StartsWith(accountPrefix)) {
					Guid accountId = Guid.Parse(key.Substring(accountPrefix.Length));
					selectedAccounts.Add(accountId,true);
				}
			}
			return selectedAccounts;
		}
		public string SearchTransactions(HttpListenerContext ctx) {
			List<BankAccount> accounts=model.GetBankAccounts();
			Dictionary<Guid,Boolean> selectedAccounts = GetSelectedAccounts(ctx,accounts);
			Dictionary<Guid,BankAccount> bankAccountsHash=model.ListToDictionary<BankAccount>(accounts);

			string minDate = ctx.Request.QueryString ["minDate"];
			string maxDate = ctx.Request.QueryString ["maxDate"];

			string maxValue=ctx.Request.QueryString ["maxValue"];
			List<object> transactions=model.FindTransactions(selectedAccounts,
				Convert.ToDecimal(ctx.Request.QueryString["minValue"]),
				Convert.ToDecimal(maxValue),
				(minDate=="")?(DateTime?)null:Convert.ToDateTime(minDate),
				(maxDate=="")?(DateTime?)null:Convert.ToDateTime(maxDate),
				ctx.Request.QueryString ["description"]
			);

			SearchTransactionsPage searchPage=new SearchTransactionsPage{
				Transactions=transactions,
				BankAccounts=bankAccountsHash,
				Accounts=accounts,SelectedAccounts=selectedAccounts,
				QueryString=ctx.Request.QueryString,
				PathAndQuery=ctx.Request.Url.PathAndQuery
			};

			return searchPage.TransformText();
		}


		public byte[] GetResource(String resourceId) {
			System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
			System.IO.Stream sourceStream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(assembly.GetName().Name+"."+resourceId);
			using(var memoryStream = new MemoryStream())
			{
				sourceStream.CopyTo(memoryStream);
				return memoryStream.ToArray();
			}
		}
		public Dictionary<String,String> GetPost(HttpListenerContext ctx) {
			Dictionary<String,String> hash = new Dictionary<String,String>();
			string body = new StreamReader(ctx.Request.InputStream).ReadToEnd();
			string[] strs=body.Split(new char[1]{'&'});
			char[] equal=new char[1]{'='};
			foreach(string str in strs) {
				string[] nv=str.Split(equal,2);
				if(nv.Length != 2)
					continue;
				if(hash.ContainsKey(nv [0]))
					continue;
				hash.Add(Uri.UnescapeDataString(nv[0]),	Uri.UnescapeDataString(nv[1]));
			}
			return hash;
		}
		public byte[] DoImport(HttpListenerContext ctx) {
			Dictionary<String,String> post=GetPost(ctx);
			var data=Json.JsonParser.Deserialize(post ["data"]);
			ManagerIO_Sqlite.Model.ImportResult result=model.Import(
				Guid.Parse(post["account"]),
				data,
				post.ContainsKey("execute")?true:false
			);
			return SendJson<ManagerIO_Sqlite.Model.ImportResult>(ctx,result);
		}
		public string ImportPage(HttpListenerContext ctx) {
			List<BankAccount> accounts=model.GetBankAccounts();
			ImportPage importPage = new ImportPage {
				Accounts=accounts
			};

			return importPage.TransformText();
//			return GetResource("html.Import.html");
		}

		private byte[] SendJson<T>(HttpListenerContext ctx, T obj) {
			string json = JsonParser.Serialize<T>(obj);
			ctx.Response.ContentType="application/json; charset=utf-8;";
			byte[] buf= System.Text.Encoding.UTF8.GetBytes(json);
			ctx.Response.ContentLength64 = buf.Length;
			return buf;
		}

		public byte[] SendResponse(HttpListenerContext ctx)
		{
			string html=null;
			byte[] htmlBytes = null;
			if(ctx.Request.Url.AbsolutePath == "/DoImport") {
				return DoImport(ctx);
			} else if(ctx.Request.Url.AbsolutePath == "/Import") {
				html = ImportPage(ctx);
			} else if(ctx.Request.Url.AbsolutePath == "/ConvertToTransfer") {
				html= ConvertToTransfer(ctx);
			} else if(ctx.Request.Url.AbsolutePath == "/SearchTransactions") {
				html= SearchTransactions(ctx);
			} else {
				html= SendAccountsResponse(ctx);
			}
			ctx.Response.ContentType="text/html; charset=utf-8;";
			if(htmlBytes==null)
				htmlBytes = System.Text.Encoding.UTF8.GetBytes(html);
			ctx.Response.ContentLength64 = htmlBytes.Length;
			return htmlBytes;
		}
	}
}

