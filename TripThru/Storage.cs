using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.DataAnnotations;

namespace TripThruCore.Models
{
    public interface Storage
    {
        void CreatePartnerAccount(PartnerAccount account);
        void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl);
        List<PartnerAccount> GetPartnerAccounts();
    }

    public class SqliteStorage : Storage
    {
        OrmLiteConnectionFactory dbFactory;
        public SqliteStorage(string sqliteFile)
        {
            dbFactory = new OrmLiteConnectionFactory(
                sqliteFile, false, SqliteDialect.Provider);
            using (var db = dbFactory.Open())
            {
                db.CreateTableIfNotExists<PartnerAccount>();
            }
        }
        public void CreatePartnerAccount(PartnerAccount account)
        {
            using (var db = dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>(x => x.ClientId == account.ClientId);
                if (acc.Count == 0)
                    db.Insert(account);
            }
        }
        public void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl)
        {
            using (var db = dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>(x => x.ClientId == account.ClientId);
                if (acc.Count == 0)
                    return;
                var existingAccount = acc.First();
                existingAccount.PartnerName = partnerName;
                existingAccount.CallbackUrl = callbackUrl;
                db.Update(existingAccount);
            }
        }
        public List<PartnerAccount> GetPartnerAccounts()
        {
            using (var db = dbFactory.Open())
            {
                return db.Select<PartnerAccount>();
            }
        }
    }

    [Alias("Account")]
    public class PartnerAccount
    {
        [AutoIncrement]
        [PrimaryKey]
        public Int32 Id { get; set; }
        public string ClientId { get; set; } //Provided by TripThru upon registration
        public string ClientSecret { get; set; } //Provided by TripThru upon registration
        public string UserName { get; set; } //For web login
        public string Password { get; set; } //For web login
        public string Email { get; set; } //For web login
        public string AccessToken { get; set; } //For them to authenticate with them
        public string RefreshToken { get; set; }
        public string PartnerName { get; set; }
        public string CallbackUrl { get; set; }
        public string TripThruAccessToken { get; set; } //For us to authenticate with them
    }

    public class StorageManager
    {

        private static Storage _storage;
        public static void OpenStorage(Storage storage){
            _storage = storage;
        }
        public static void CreatePartnerAccount(PartnerAccount account)
        {
            if (_storage == null)
                return;
            _storage.CreatePartnerAccount(account);
        }
        public static void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl)
        {
            if (_storage == null)
                return;
            _storage.RegisterPartner(account, partnerName, callbackUrl);
        }
        public static List<PartnerAccount> GetPartnerAccounts()
        {
            if (_storage == null)
                return null;
            return _storage.GetPartnerAccounts();
        }
    }
}
