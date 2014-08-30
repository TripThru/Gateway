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
        public abstract void UpdateTrip(Trip trip);
        public abstract List<Trip> GetTripsByState(TripState state);
        public abstract List<Trip> GetDirtyTrips();
        public abstract Route GetRoute(string id);
        public abstract void UpdateRoute(Route route);
        public abstract void InsertQuote(TripQuotes quote);
        public abstract void UpdateQuote(TripQuotes quote);
        public abstract TripQuotes GetQuote(string tripId);
        public abstract List<TripQuotes> GetQuotesByStatus(QuoteStatus status);
        public abstract void Reset();
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
            throw new NotImplementedException();
        }
        public override void UpdateTrip(Trip trip)
        {
            throw new NotImplementedException();
        }
        public override List<Trip> GetTripsByState(TripState state)
        {
            throw new NotImplementedException();
        }
        public override List<Trip> GetDirtyTrips()
        {
            throw new NotImplementedException();
        }
        public override Route GetRoute(string id)
        {
            throw new NotImplementedException();
        }
        public override void UpdateRoute(Route route)
        {
            throw new NotImplementedException();
        }
        public override void InsertQuote(TripQuotes quote)
        {
            throw new NotImplementedException();
        }
        public override void UpdateQuote(TripQuotes quote)
        {
            throw new NotImplementedException();
        }
        public override TripQuotes GetQuote(string tripId)
        {
            throw new NotImplementedException();
        }
        public override List<TripQuotes> GetQuotesByStatus(QuoteStatus status)
        {
            throw new NotImplementedException();
        }
        public override void Reset()
        {
            throw new NotImplementedException();
        }
    }

    public class MongoDbStorage : Storage
    {
        private readonly MongoCollection<PartnerAccount> _partners;
        private readonly MongoCollection<Trip> _trips;
        private readonly MongoCollection<Route> _routes;
        private readonly MongoCollection<TripQuotes> _quotes;
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
                cm.GetMemberMap(c => c.IdNumber).SetIgnoreIfNull(true);
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
                cm.GetMemberMap(c => c.LatenessMilliseconds).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.Creation).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.loc);
                cm.GetMemberMap(c => c.SamplingPercentage);
                cm.GetMemberMap(c => c.State).SetRepresentation(BsonType.String);
                cm.GetMemberMap(c => c.IsDirty);
                cm.GetMemberMap(c => c.MadeDirtyById);
            });
            MongoDB.Bson.Serialization.BsonClassMap.RegisterClassMap<Route>(cm =>
            {
                cm.AutoMap();
                foreach (var mm in cm.AllMemberMaps)
                    mm.SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.waypoints).SetIgnoreIfNull(true);
            });
            MongoDB.Bson.Serialization.BsonClassMap.RegisterClassMap<TripQuotes>(cm =>
            {
                cm.AutoMap();
                foreach (var mm in cm.AllMemberMaps)
                    mm.SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.Status).SetRepresentation(BsonType.String);
                cm.GetMemberMap(c => c.QuoteRequest).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.ReceivedQuotes).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.PartnersThatServe).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.Autodispatch).SetIgnoreIfNull(true);
                cm.GetMemberMap(c => c.ReceivedUpdatesCount).SetIgnoreIfNull(true);
            });
            var server = MongoServer.Create(tripsDatabaseConnectionString);

            _networksDatabase = server.GetDatabase(_networksDatabaseId);
            _partners = _networksDatabase.GetCollection<PartnerAccount>("users");

            _tripsDatabase = server.GetDatabase(RemoveSpecialCharacters(tripsDatabaseName));
            _trips = _tripsDatabase.GetCollection<Trip>("trips");
            _routes = _tripsDatabase.GetCollection<Route>("routes");
            _quotes = _tripsDatabase.GetCollection<TripQuotes>("quotes");
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
            if (_tripsDatabaseId == "TripThru") //Only partners need to retrieve latest id to generate trips
                return 0;
            var lastTrip = _trips.FindAll()
                .SetSortOrder(SortBy.Descending("IdNumber"))
                .Where(t => t.Id.Contains(this._tripsDatabaseId))
                .FirstOrDefault();
            return lastTrip != null && lastTrip.IdNumber != null ? (long)lastTrip.IdNumber : 0;
        }
        public override void InsertTrip(Trip trip)
        {
            this._trips.Insert(trip);
        }
        public override void UpdateTrip(Trip trip)
        {
            this._trips.Save(trip);
        }
        public override List<Trip> GetTripsByState(TripState state)
        {
            return _trips.AsQueryable<Trip>().Where(t => t.State == state).ToList();
        }
        public override List<Trip> GetDirtyTrips()
        {
            return _trips.AsQueryable<Trip>().Where(t => t.IsDirty == true).ToList();
        }
        public override Route GetRoute(string id)
        {
            var routes = this._routes.AsQueryable<Route>().Where(r => r.Id == id).ToList();
            if (routes.Count > 0)
                return routes[0];
            else
                return null;
        }
        public override void UpdateRoute(Route route)
        {
            this._routes.Save(route);
        }
        public override void InsertQuote(TripQuotes quote)
        {
            this._quotes.Insert(quote);
        }
        public override void UpdateQuote(TripQuotes quote)
        {
            this._quotes.Save(quote);
        }
        public override TripQuotes GetQuote(string tripId)
        {
            var quotes = this._quotes.AsQueryable<TripQuotes>().Where(q => q.Id == tripId).ToList();
            if (quotes.Count > 0)
                return quotes[0];
            else
                return null;
        }
        public override List<TripQuotes> GetQuotesByStatus(QuoteStatus status)
        {
            return _quotes.AsQueryable<TripQuotes>().Where(q => q.Status == status).ToList();
        }
        public override void Reset()
        {
            this._trips.Drop();
            this._quotes.Drop();
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
        public List<Zone> Coverage { set; get; }
    }

    public class StorageManager
    {

        private static Storage _storage;
        public static void OpenStorage(Storage storage){
            _storage = storage;
        }
        public static void CreatePartnerAccount(PartnerAccount account)
        {
            try {
                if (_storage == null)
                    return;
                _storage.CreatePartnerAccount(account);
            }
            catch (Exception e)
            {
                Logger.LogDebug("CreatePartnerAccount exception", e.ToString());
                throw e;
            }
        }
        public static void RegisterPartner(PartnerAccount account, string partnerName, string callbackUrl)
        {
            try{
                if (_storage == null)
                    return;
                _storage.RegisterPartner(account, partnerName, callbackUrl);
            }
            catch (Exception e)
            {
                Logger.LogDebug("RegisterPartner exception", e.ToString());
                throw e;
            }
        }
        public static IEnumerable<PartnerAccount> GetPartnerAccounts()
        {
            try { 
                if (_storage == null)
                    return null;
                return _storage.GetPartnerAccounts();
                }
            catch (Exception e)
            {
                Logger.LogDebug("GetPartnerAccounts exception", e.ToString());
                throw e;
            }
        }

        public static PartnerAccount GetPartnerAccountByUsername(string userName)
        {
            try {
                if (_storage == null)
                    return null;
                return _storage.GetPartnerAccountByUsername(userName);
            }
            catch (Exception e)
            {
                Logger.LogDebug("GetPartnerAccountByUsername exception", e.ToString());
                throw e;
            }
        }
        public static PartnerAccount GetPartnerAccountByClientId(string clientId)
        {
            try {
                if (_storage == null)
                    return null;
                return _storage.GetPartnerAccountByClientId(clientId);
            }
            catch (Exception e)
            {
                Logger.LogDebug("GetPartnerAccountByClientId exception", e.ToString());
                throw e;
            }
        }
        public static PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            try {
                if (_storage == null)
                    return null;
                return _storage.GetPartnerAccountByAccessToken(accessToken);
            }
            catch (Exception e)
            {
                Logger.LogDebug("GetPartnerAccountByAccessToken exception", e.ToString());
                throw e;
            }
        }
        public static long GetLastTripId()
        {
            try {
                if (_storage == null)
                    return 0;
                return _storage.GetLastTripId();
            }
            catch (Exception e)
            {
                Logger.LogDebug("GetLastTripId exception", e.ToString());
                throw e;
            }
        }
        public static void InsertTrip(Trip trip)
        {
            try {
                if (_storage != null)
                    _storage.InsertTrip(trip);
                }
            catch (Exception e)
            {
                Logger.LogDebug("InsertTrip exception", e.ToString());
                throw e;
            }
        }
        public static void UpdateTrip(Trip trip)
        {
            try { 
                if (_storage != null)
                _storage.UpdateTrip(trip);
                }
            catch (Exception e)
            {
                Logger.LogDebug("UpdateTrip exception", e.ToString());
                throw e;
            }
        }
        public static List<Trip> GetTripsByState(TripState state)
        {
            try{
                if (_storage != null)
                    return _storage.GetTripsByState(state);
                else
                    return null;
                }
            catch (Exception e)
            {
                Logger.LogDebug("GetTripsByState exception", e.ToString());
                throw e;
            }
        }
        public static List<Trip> GetDirtyTrips()
        {
            try {
                if (_storage != null)
                    return _storage.GetDirtyTrips();
                else
                    return null;
            }
            catch (Exception e)
            {
                Logger.LogDebug("GetDirtyTrips exception", e.ToString());
                throw e;
            }
        }
        public static Route GetRoute(string id)
        {
            try {
                if (_storage != null)
                    return _storage.GetRoute(id);
                else
                    return null;
            }
            catch (Exception e)
            {
                Logger.LogDebug("GetRoute exception", e.ToString());
                throw e;
            }
        }
        public static void UpdateRoute(Route route)
        {
            try {
                if (_storage != null)
                    _storage.UpdateRoute(route);
            }
            catch (Exception e)
            {
                Logger.LogDebug("UpdateRoute exception", e.ToString());
                throw e;
            }
        }
        public static void InsertQuote(TripQuotes quote)
        {
            try {
                if (_storage != null)
                    _storage.InsertQuote(quote);
            }
            catch (Exception e)
            {
                Logger.LogDebug("InsertQuote exception", e.ToString());
                throw e;
            }
        }
        public static void UpdateQuote(TripQuotes quote)
        {
            try{
                if (_storage != null)
                    _storage.UpdateQuote(quote);
                }
            catch (Exception e)
            {
                Logger.LogDebug("UpdateQuote exception", e.ToString());
                throw e;
            }
        }
        public static TripQuotes GetQuote(string tripId)
        {
            try{
                if (_storage != null)
                    return _storage.GetQuote(tripId);
                else
                    return null;
                }
            catch (Exception e)
            {
                Logger.LogDebug("GetQuote exception", e.ToString());
                throw e;
            }
        }
        public static List<TripQuotes> GetQuotesByStatus(QuoteStatus status)
        {
            try
            {
                if (_storage != null)
                    return _storage.GetQuotesByStatus(status);
                else
                    return null;
            }
            catch (Exception e)
            {
                Logger.LogDebug("GetQuotesByStatus exception", e.ToString());
                throw e;
            }
        }
        public static void Reset()
        {
            try{
                if (_storage != null)
                    _storage.Reset();
            }
            catch(Exception e)
            {
                Logger.LogDebug("Storage Reset exception", e.ToString());
                throw e;
            }
        }
    }
}
