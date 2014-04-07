using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;

namespace TripThruCore.Models
{
    public interface Storage
    {
        void CreatePartnerAccount(PartnerAccount account);
        bool RegisterPartner(PartnerAccount account, Partner partner);
    }

    public class SqliteStorage : Storage
    {
        OrmLiteConnectionFactory dbFactory;
        public SqliteStorage(string sqliteFile)
        {
            dbFactory = new OrmLiteConnectionFactory(
                sqliteFile, false, SqliteDialect.Provider);

        }
        private void Init()
        {
            using(var db = dbFactory.Open()){
                db.CreateTableIfNotExists<PartnerAccount>();
                db.CreateTableIfNotExists<Partner>();
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
        public bool RegisterPartner(PartnerAccount account, Partner partner)
        {
            using (var db = dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>(x => x.ClientId == account.ClientId);
                if (acc.Count == 0)
                    return false;
                var part = db.Select<Partner>(x => x.ClientId == partner.ClientId);
                if (part.Count == 0)
                    db.Insert(partner);
                else
                    db.Update(partner);
                return true;
            }
        }
    }

    public class PartnerAccount
    {
        public string Name { get; set; }
        public string ClientId { get; set; } //Provided by TripThru upon registration
        public string ClientSecret { get; set; } //Provided by TripThru upon registration
        public string UserName { get; set; } //For web login
        public string Password { get; set; } //For web login
        public string Email { get; set; } //For web login
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string CallbackUrl { get; set; }
    }

    public class Partner
    {
        public string ClientId { get; set; }
        public string Name { get; set; }
        public string CallbackUrl { get; set; }
    }
}
