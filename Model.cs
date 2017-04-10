﻿using System;
using Mono.Data.Sqlite;
using System.Data;
using Manager.Model;
using System.Collections.Generic;
using System.Reflection;

namespace ManagerIO_Sqlite
{
	public class Model
	{
		IDbConnection dbcon;
		public delegate void ChangeEvent();
		public event ChangeEvent PreChangeEvents;
		IDbTransaction transaction=null;

		public Model ()
		{

		}



		public List<Type> FindByType<Type>() {
			Guid guid=Manager.Serialization.GetGuidByType(typeof(Type));
			IDbCommand dbcmd = CreateCommand();
			dbcmd.CommandText = String.Format("select * from Objects where ContentType = '{0}'",guid.ToString());
			IDataReader reader = dbcmd.ExecuteReader();
			List<Type> objs= new List<Type>();

			while(reader.Read())
			{
				long byteSize= reader.GetBytes (2,0,null,0,0);
				byte[] b=new byte[byteSize];
				reader.GetBytes (2,0,b,0,(int)byteSize);
				string guidStr = reader.GetString(0);
				string typeGuidStr = reader.GetString(1);
				object obj=Manager.Serialization.Deserialize( Guid.Parse(typeGuidStr), b);
				if(obj is Manager.Model.Object) {
					((Manager.Model.Object)obj).Key=Guid.Parse(guidStr);
				}
				objs.Add((Type)obj);
			}
			// clean up
			reader.Dispose();
			dbcmd.Dispose();
			return objs;
		}
		public List<Payment> GetPayments() {
			return FindByType<Payment>();
		}
		public List<Receipt> GetReceipts() {
			return FindByType<Receipt>();
		}
		public List<CashAccount2> GetCashAccounts() {
			return FindByType<Manager.Model.CashAccount2>();
		}

		public class PaymentReceipt
		{
			public Payment Payment { get; set; }
			public Receipt Receipt {get; set; }
			public PaymentReceipt() { }
		}

		public Dictionary<Guid,Type> ListToDictionary<Type>(List<Type> list) {
			Dictionary<Guid,Type> dict = new Dictionary<Guid,Type>();
			foreach(Type item in list) {
				if(item is Manager.Model.Object) {
					Guid guid=((Manager.Model.Object)(item as Manager.Model.Object)).Key;
					dict.Add(guid,item);
				}
			}
			return dict;
		}

		public List<PaymentReceipt> FindTransfers(
			Dictionary<Guid,Boolean> accountGuids,
			decimal min, decimal max,
			double maxDays
		) {
			List<Receipt> receipts = GetReceipts();
			List<Payment> payments = GetPayments();
			List <PaymentReceipt> paymentReceiptList= new List<PaymentReceipt>();
			Dictionary<Guid,Boolean> done = new Dictionary<Guid,Boolean>();
			/*
			Dictionary<Guid,Boolean> accountsHash = new Dictionary<Guid,Boolean>();
			foreach(Guid accountGuid in accountGuids) {
				accountsHash.Add(accountGuid, true);
			}
			*/

			foreach(Receipt receipt in receipts) {
				if(!receipt.DebitAccount.HasValue || !accountGuids.ContainsKey(receipt.DebitAccount.Value)) {
					continue;
				}
				if(receipt.Lines.Length != 1)
					continue;
				if(receipt.Lines [0].Account != null)
					continue;
				Decimal receiptAmount=receipt.Lines [0].Amount;
				foreach(Payment payment in payments) {
					if(!payment.CreditAccount.HasValue || !accountGuids.ContainsKey(payment.CreditAccount.Value)) {
						continue;
					}
					if(payment.CreditAccount == receipt.DebitAccount)
						continue;
					if(payment.Lines.Length != 1)
						continue;
					if(payment.Lines [0].Account != null)
						continue;
					if(done.ContainsKey(payment.Key) || done.ContainsKey(receipt.Key))
						continue;
					Decimal paymentAmount=payment.Lines [0].Amount;
					decimal diff=(receiptAmount - paymentAmount )/paymentAmount;
					if(diff>max || diff<min)
						continue;
					
					double daysDiff= (receipt.Date - payment.Date).TotalDays;
					if((maxDays>=0)?  
						(daysDiff>=0 && daysDiff<=maxDays) : 
						(daysDiff<0 && daysDiff>=maxDays)
					) {
						paymentReceiptList.Add(new PaymentReceipt(){ Payment = payment, Receipt = receipt });
						done.Add(payment.Key,true);
						done.Add(receipt.Key,true);
					}
				}
			}
			paymentReceiptList.Sort((x, y) => x.Payment.Date.CompareTo(y.Payment.Date));
			return paymentReceiptList;
		}

		public object GetObject(Guid guid) {
			IDbCommand dbcmd = CreateCommand();
			dbcmd.CommandText = String.Format("select * from Objects where Key = '{0}'",guid.ToString());
			IDataReader reader = dbcmd.ExecuteReader();
			//List<Type> objs= new List<Type>();

			object obj=null;
			while(reader.Read()) {
				long byteSize= reader.GetBytes (2,0,null,0,0);
				byte[] b=new byte[byteSize];
				reader.GetBytes (2,0,b,0,(int)byteSize);
				string guidStr = reader.GetString(0);
				string typeGuidStr = reader.GetString(1);
				obj=Manager.Serialization.Deserialize( Guid.Parse(typeGuidStr), b);
				if(obj is Manager.Model.Object) {
					(obj as Manager.Model.Object).Key = Guid.Parse(guidStr);
				}
			}
			reader.Dispose();
			dbcmd.Dispose();
			return obj;
		}
		public void DeleteObject(Guid guid) {
			PreChangeEvents();
			IDbCommand dbcmd = CreateCommand();
			dbcmd.CommandText = String.Format("delete from Objects where Key = '{0}'",guid.ToString());
			dbcmd.ExecuteNonQuery();
			dbcmd.Dispose();
		}

		IDbCommand CreateCommand() {
			IDbCommand dbcmd = dbcon.CreateCommand();
			if(transaction!=null) dbcmd.Transaction = transaction;
			return dbcmd;
		}
		public void BeginTransaction() {
			transaction=dbcon.BeginTransaction();
		}
		public void CloseTransaction() {
			if(transaction == null)
				return;
			transaction.Commit();
			transaction = null;
		}

		public void InsertObject(Manager.Model.Object obj) {
			Guid guid=obj.Key;
			obj.Key=Guid.Empty;
			Tuple<Guid,Byte[]> tuple=Manager.Serialization.Serialize(obj);
			IDbCommand dbcmd = CreateCommand();
			dbcmd.CommandText = String.Format("insert into Objects (Key,ContentType,Content) values(@key,@type,@BIN)");

			IDbDataParameter param = dbcmd.CreateParameter();
			param.DbType = DbType.AnsiString;
			param.ParameterName = "@key";
			param.Value = guid.ToString();
			dbcmd.Parameters.Add(param);
			param = dbcmd.CreateParameter();
			param.DbType = DbType.AnsiString;
			param.ParameterName = "@type";
			param.Value = Manager.Serialization.GetGuidByType(obj.GetType()).ToString();
			dbcmd.Parameters.Add(param);
			param = dbcmd.CreateParameter();
			param.DbType = DbType.Binary;
			param.ParameterName = "@BIN";
			param.Value = tuple.Item2;
			dbcmd.Parameters.Add(param);

			//dbcmd.Parameters.Add("@BIN", SqlDbType.Binary, tuple.Item2.Length).Value = tuple.Item2;

			dbcmd.ExecuteNonQuery();
			dbcmd.Dispose();
		}
		public void ConvertToTransfer(PaymentReceipt paymentReceipt) {
			PreChangeEvents();
			Manager.Model.Transfer transfer=new Manager.Model.Transfer();
			transfer.CreditAccount = paymentReceipt.Payment.CreditAccount;
			transfer.DebitAccount = paymentReceipt.Receipt.DebitAccount;
			transfer.CreditAmount = paymentReceipt.Payment.Lines [0].Amount;
			transfer.DebitAmount = paymentReceipt.Receipt.Lines [0].Amount;
			transfer.CreditClearDate = paymentReceipt.Payment.BankClearDate!=null?paymentReceipt.Payment.BankClearDate:paymentReceipt.Payment.Date;
			transfer.DebitClearDate = paymentReceipt.Receipt.BankClearDate!=null?paymentReceipt.Receipt.BankClearDate:paymentReceipt.Receipt.Date;
			transfer.CreditClearStatus = paymentReceipt.Payment.BankClearStatus;
			transfer.DebitClearStatus = paymentReceipt.Receipt.BankClearStatus;
			transfer.Date = paymentReceipt.Payment.Date;
			transfer.Description = paymentReceipt.Payment.Description + ", " + paymentReceipt.Receipt.Description;


			transfer.Key = Guid.NewGuid();
			InsertObject(transfer);
			DeleteObject(paymentReceipt.Payment.Key);
			DeleteObject(paymentReceipt.Receipt.Key);
		}

		#region Init
		public void Open(string connectionString) {
			dbcon = new SqliteConnection(connectionString);
			dbcon.Open();
		}
		public void Close() {
			CloseTransaction();
			dbcon.Close();
		}
		#endregion
	}
}
