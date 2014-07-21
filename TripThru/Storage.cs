using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.GridFS;
using MongoDB.Driver.Linq;
using ServiceStack.Text;
using Utils;

namespace TripThruCore.Storage
{
    public abstract class Storage
    {
        public enum UserRole { admin, partner, demo }
        public abstract void CreatePartnerAccount(PartnerAccount account);
        public abstract void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl);
        public abstract IEnumerable<PartnerAccount> GetPartnerAccounts();
        public abstract PartnerAccount GetPartnerAccountByUsername(string userName);
        public abstract PartnerAccount GetPartnerAccountByClientId(string clientId);
        public abstract PartnerAccount GetPartnerAccountByAccessToken(string accessToken);
        public abstract long GetLastTripId();
        public abstract void InsertTrip(Trip trip);
        public abstract void SaveTrip(Trip trip);
        public abstract Route GetRoute(string id);
        public abstract void SaveRoute(Route route);
        protected string RemoveSpecialCharacters(string input)
        {
            return new string(input.Where(c => Char.IsLetterOrDigit(c)).ToArray());
        }
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
        public override IEnumerable<PartnerAccount> GetPartnerAccounts()
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
        public override PartnerAccount GetPartnerAccountByClientId(string clientId)
        {
            using (var db = dbFactory.Open())
            {
                var acc = db.Select<PartnerAccount>().Where(x => x.ClientId == clientId);
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
        public override long GetLastTripId()
        {
            return 0;
        }
        public override void InsertTrip(Trip trip)
        {
            
        }
        public override void SaveTrip(Trip trip)
        {
            
        }
        public override Route GetRoute(string id)
        {
            throw new NotImplementedException();
        }
        public override void SaveRoute(Route route)
        {
            throw new NotImplementedException();
        }
    }

    public class MongoDbStorage : Storage
    {
        private readonly MongoCollection<PartnerAccount> _partners;
        private readonly MongoCollection<Trip> _trips;
        private readonly MongoCollection<Route> _routes;
        private MongoDatabase _tripsDatabase;
        private readonly string _tripsDatabaseId;
        private MongoDatabase _networksDatabase;
        private readonly string _networksDatabaseId = "TripThru";
        public MongoDbStorage(string tripsDatabaseConnectionString, string tripsDatabaseName)
        {
            _tripsDatabaseId = tripsDatabaseName;
            MongoDB.Bson.Serialization.BsonClassMap.RegisterClassMap<Trip>(cm =>
            {
                cm.AutoMap();
                foreach (var mm in cm.AllMemberMaps)
                    mm.SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.Status).SetRepresentation(BsonType.String);
                cm.GetMemberMap(c => c.PickupTime).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.PickupLocation).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.FleetId).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.FleetName).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.ETA).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.DropoffLocation).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.DropoffTime).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.DriverRouteDuration).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.DriverId).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.OccupiedDistance).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.DriverName).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.Price).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.ServicingPartnerName).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.ServicingPartnerId).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.VehicleType).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.DriverLocation).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.DriverInitiaLocation).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.LastUpdate).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.Lateness).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.loc);
            });
            MongoDB.Bson.Serialization.BsonClassMap.RegisterClassMap<Route>(cm =>
            {
                cm.AutoMap();
                foreach (var mm in cm.AllMemberMaps)
                    mm.SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.waypoints).SetIgnoreIfNull(true);
            });
            var server = MongoServer.Create(tripsDatabaseConnectionString);

            _networksDatabase = server.GetDatabase(_networksDatabaseId);
            _partners = _networksDatabase.GetCollection<PartnerAccount>("users");

            _tripsDatabase = server.GetDatabase(RemoveSpecialCharacters(tripsDatabaseName));
            _trips = _tripsDatabase.GetCollection<Trip>("trips");
            _routes = _tripsDatabase.GetCollection<Route>("routes");
        }
        public override void CreatePartnerAccount(PartnerAccount account)
        {
            _partners.Insert(account);
        }

        public override void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl)
        {
            var query = Query<PartnerAccount>.EQ(e => e.ClientId, account.ClientId);
            var entity = _partners.FindOne(query);
            if(entity == null)
                return;
            var update = Update<PartnerAccount>.Set(e => e.PartnerName, partnerName);
            _partners.Update(query, update);
            update = Update<PartnerAccount>.Set(e => e.CallbackUrl, callbackUrl);
            _partners.Update(query, update);
            entity.CallbackUrl = callbackUrl;
        }

        public override IEnumerable<PartnerAccount> GetPartnerAccounts()
        {
            IEnumerable<PartnerAccount> networks = _partners.FindAll();
            return networks;
        }

        public override PartnerAccount GetPartnerAccountByUsername(string userName)
        {
            var query = Query<PartnerAccount>.EQ(e => e.UserName, userName);
            var network = _partners.FindOne(query);
            return network;
        }

        public override PartnerAccount GetPartnerAccountByClientId(string clientId)
        {
            var query = Query<PartnerAccount>.EQ(e => e.ClientId, clientId);
            var network = _partners.FindOne(query);
            return network;
        }
        public override PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            var query = Query<PartnerAccount>.EQ(e => e.AccessToken, accessToken);
            var network = _partners.FindOne(query);
            return network;
        }

        public override long GetLastTripId()
        {
            var lastTrips = _trips.AsQueryable<Trip>()
                .Where(c => c.Id.Contains(this._tripsDatabaseId)).OrderByDescending(c => c.LastUpdate).ThenByDescending(c => c.Id);
            //To do: Find a proper way to query the last Id
            long maxId = 0;
            var limit = 30;
            foreach (var trip in lastTrips)
            {
                var tripId = long.Parse(trip.Id.SplitOnFirst('@')[0]);
                if (tripId > maxId)
                    maxId = tripId;
                if (--limit == 0)
                    break;
            }
            return maxId;
        }
        public override void InsertTrip(Trip trip)
        {
            this._trips.Insert(trip);
        }
        public override void SaveTrip(Trip trip)
        {
            this._trips.Save(trip);
        }
        public override Route GetRoute(string id)
        {
            var routes = this._routes.AsQueryable<Route>().Where(r => r.Id == id).ToList();
            if (routes.Count > 0)
                return routes[0];
            else
                return null;
        }
        public override void SaveRoute(Route route)
        {
            this._routes.Save(route);
        }
    }

    [Alias("Account")]
    [BsonIgnoreExtraElements]
    public class PartnerAccount
    {
        [AutoIncrement]
        [PrimaryKey]
        [BsonIgnore]
        public Int32 Id { get; set; }
        public string ClientId { get; set; } //Provided by TripThru upon registration
        public string UserName { get; set; } //For web login
        public string AccessToken { get; set; } //For them to authenticate with them
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
        public static IEnumerable<PartnerAccount> GetPartnerAccounts()
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
        public static PartnerAccount GetPartnerAccountByClientId(string clientId)
        {
            if (_storage == null)
                return null;
            return _storage.GetPartnerAccountByClientId(clientId);
        }
        public static PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            if (_storage == null)
                return null;
            return _storage.GetPartnerAccountByAccessToken(accessToken);
        }
        public static long GetLastTripId()
        {
            if (_storage == null)
                return 0;
            return _storage.GetLastTripId();
        }
        public static void InsertTrip(Trip trip)
        {
            if (_storage != null)
                _storage.InsertTrip(trip);
        }
        public static void SaveTrip(Trip trip)
        {
            if (_storage != null)
                _storage.SaveTrip(trip);
        }
        public static Route GetRoute(string id)
        {
            if (_storage != null)
                return _storage.GetRoute(id);
            else
                return null;
        }
        public static void SaveRoute(Route route)
        {
            if (_storage != null)
                _storage.SaveRoute(route);
        }
    }
}
