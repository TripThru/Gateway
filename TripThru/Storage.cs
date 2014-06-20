using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.DataAnnotations;

namespace TripThruCore.Storage
{
    public abstract class Storage
    {
        public enum UserRole { admin, partner, user }
        public abstract void CreatePartnerAccount(PartnerAccount account);
        public abstract void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl);
        public abstract List<PartnerAccount> GetPartnerAccounts();
        public abstract PartnerAccount GetPartnerAccountByUsername(string userName);
        public abstract PartnerAccount GetPartnerAccountByAccessToken(string accessToken);
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
        public override void CreatePartnerAccount(PartnerAccount account)
        {
            using (var db = dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>(x => x.ClientId == account.ClientId);
                if (acc.Count == 0)
                    db.Insert(account);
            }
        }
        public override void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl)
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
        public override List<PartnerAccount> GetPartnerAccounts()
        {
            using (var db = dbFactory.Open())
            {
                return db.Select<PartnerAccount>();
            }
        }
        public override PartnerAccount GetPartnerAccountByUsername(string userName)
        {
            using (var db = dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>().Where(x => x.UserName == userName);
                return acc.Count() == 1 ? acc.First() : null;
            }
        }
        public override PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            using (var db = dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>().Where(x => x.AccessToken == accessToken);
                return acc.Count() == 1 ? acc.First() : null;
            }
        }
    }

    public class MongoDbStorage : Storage
    {
        private readonly MongoCollection<PartnerAccount> _mongo;
        private MongoDatabase _database;

        public MongoDbStorage(string databaseName)
        {
            var server = MongoServer.Create("mongodb://SG-TripThru-2816.servers.mongodirector.com:27017/");
            _database = server.GetDatabase(databaseName);
            _mongo = _database.GetCollection<PartnerAccount>("networks");
        }
        public override void CreatePartnerAccount(PartnerAccount account)
        {
            _mongo.Insert(account);
        }

        public override void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl)
        {
            var query = Query<PartnerAccount>.EQ(e => e.Id, account.Id);
            var entity = _mongo.FindOne(query);
            if(entity == null)
                return;
            var update = Update<PartnerAccount>.Set(e => e.PartnerName, partnerName);
            _mongo.Update(query, update);
            update = Update<PartnerAccount>.Set(e => e.CallbackUrl, callbackUrl);
            _mongo.Update(query, update);
            entity.CallbackUrl = callbackUrl;
        }

        public override List<PartnerAccount> GetPartnerAccounts()
        {
            var networks = _mongo.FindAll();
            return networks.ToList();
        }

        public override PartnerAccount GetPartnerAccountByUsername(string userName)
        {
            var query = Query<PartnerAccount>.EQ(e => e.UserName, userName);
            var network = _mongo.FindOne(query);
            return network;
        }

        public override PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            var query = Query<PartnerAccount>.EQ(e => e.AccessToken, accessToken);
            var network = _mongo.FindOne(query);
            return network;
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
