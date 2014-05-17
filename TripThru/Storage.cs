using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.DataAnnotations;

namespace TripThruCore.Storage
{
    public abstract class Storage
    {
        readonly OrmLiteConnectionFactory _dbFactory;

        public enum UserRole { admin, partner, user }

        protected Storage(OrmLiteConnectionFactory dbFactory)
        {
            this._dbFactory = dbFactory;
        }

        public virtual void CreatePartnerAccount(PartnerAccount account)
        {
            using (var db = _dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>(x => x.ClientId == account.ClientId);
                if (acc.Count == 0)
                    db.Insert(account);
            }
        }

        public virtual void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl)
        {
            using (var db = _dbFactory.Open())
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

        public virtual List<PartnerAccount> GetPartnerAccounts()
        {
            using (var db = _dbFactory.Open())
            {
                return db.Select<PartnerAccount>();
            }
        }

        public virtual PartnerAccount GetPartnerAccountByUsername(string userName)
        {
            using (var db = _dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>().Where(x => x.UserName == userName);
                return acc.Count() == 1 ? acc.First() : null;
            }
        }

        public virtual PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            using (var db = _dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>().Where(x => x.AccessToken == accessToken);
                return acc.Count() == 1 ? acc.First() : null;
            }
        }
    }

    public class SqliteStorage : Storage
    {
        public SqliteStorage(string sqliteFile) : base (new OrmLiteConnectionFactory(
                sqliteFile, false, SqliteDialect.Provider))
        {

        }
    }

    public class PostgresSql : Storage
    {
        public PostgresSql(string server, string port, string database, string userId, string password)
            : base(new OrmLiteConnectionFactory(
                String.Format("Server={0};Port={1};Database={2};User Id={3};Password={4};", server, port,
                database, userId, password), false, PostgreSqlDialect.Provider))
        {
            
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
        public Storage.UserRole Role { get; set; }
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

        public static PartnerAccount GetPartnerAccountByUsername(string userName)
        {
            if (_storage == null)
                return null;
            return _storage.GetPartnerAccountByUsername(userName);
        }
        public static PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            if (_storage == null)
                return null;
            return _storage.GetPartnerAccountByAccessToken(accessToken);
        }
    }
}
