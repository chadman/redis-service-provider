using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Collections.Specialized;
using System.Web.SessionState;
using ServiceStack.Redis;
using System.Configuration;
using System.Configuration.Provider;
using System.Web.Configuration;
using System.IO;

namespace Web.SessionProvider {

    #region Repository
    public interface ICustomServiceProviderRepository {
        void Initialize(HttpContext context);
        SessionItem GetRedisCacheItem(string id, string key);
        void AddRedisItem(string id, KeyValuePair<string, SessionItem> value);
        void UpdateRedisItem(string id, string objects);
        void Dispose();
    }
    
    public class CustomServiceProviderRepository : ICustomServiceProviderRepository {

        #region Properties
        private IRedisClient redisClient = null;
        #endregion Properties

        #region Constructor
        public CustomServiceProviderRepository() {
            redisClient = new RedisClient("127.0.0.1", 6379);
        }
        #endregion Constructor

        public void Initialize(HttpContext context) {

        }

        public void AddRedisItem(string id, KeyValuePair<string, SessionItem> value) {
            List<KeyValuePair<string, SessionItem>> values = redisClient.Get<List<KeyValuePair<string, SessionItem>>>(id);

            if (values.FindIndex(x => x.Key == value.Key) < 0) {
                values.Add(value);
                redisClient.Set<List<KeyValuePair<string, SessionItem>>>(id, values, DateTime.Now.AddMinutes(30).ToUniversalTime());
            }
        }

        public void UpdateRedisItem(string id, string objects) {
            redisClient.GetAndSetEntry(id, objects);
            redisClient.ExpireEntryAt(id, DateTime.Now.AddMinutes(30).ToUniversalTime());
        }

        public SessionItem GetRedisCacheItem(string id, string key) {

            List<KeyValuePair<string, SessionItem>> values = redisClient.Get<List<KeyValuePair<string, SessionItem>>>(id);

            if (values.FindIndex(x => x.Key == key) > -1) {
                return (SessionItem)values[values.FindIndex(x => x.Key == key)].Value;
            }
            return null;
        }

        public void Dispose() {
            redisClient.Dispose();
        }
    }
    #endregion Repository

    #region Session Item Model
    public class SessionItem {

        #region Properties
        public DateTime CreatedAt { get; set; }
        public DateTime LockDate { get; set; }
        public int LockID { get; set; }
        public int Timeout { get; set; }
        public bool Locked { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        #endregion Properties

    }
    #endregion Session Item Model

    public class CustomServiceProvider : System.Web.SessionState.SessionStateStoreProviderBase, IDisposable {

        #region Properties
        public ICustomServiceProviderRepository Repository = null;
        #endregion Properties

        #region Constructor
        public CustomServiceProvider() {
            this.Repository = new CustomServiceProviderRepository();
        }
        #endregion Constructor


        public override void Initialize(string name, NameValueCollection config) {


            // Initialize values from web.config.
            if (config == null) {
                throw new ArgumentNullException("config");
            }

            if (name == null || name.Length == 0) {
                name = "RedisSessionStateStore";
            }

            if (String.IsNullOrEmpty(config["description"])) {
                config.Remove("description");
                config.Add("description", "Redis Session State Provider");
            }

            // Initialize the abstract base class.
            base.Initialize(name, config);

            /*
            // Get <sessionState> configuration element.
            Configuration cfg = WebConfigurationManager.OpenWebConfiguration(ConfigurationManager.AppSettings["ApplicationName"]);
            _config = (SessionStateSection)cfg.GetSection("system.web/sessionState");
            */
        }

        public override void Dispose() {
            this.Repository.Dispose();
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback) {
            return true;
        }

        /// <summary>
        /// Serialize is called by the SetAndReleaseItemExclusive method to
        /// convert the SessionStateItemCollection into a Base64 string to
        /// be stored in MongoDB.
        /// </summary>
        private string Serialize(SessionStateItemCollection items) {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms)) {
                if (items != null)
                    items.Serialize(writer);

                writer.Close();

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem) {

            /*
            string sessionItems = this.Serialize((SessionStateItemCollection)item.Items);

            // If the item is new then add it to redis
            if (newItem) {
                this.Repository.AddRedisItem(id, sessionItems);
            }
            else {
                this.Repository.UpdateRedisItem(id, sessionItems);
            }
             * */

            SessionStateItemCollection sessionItems = (SessionStateItemCollection)item.Items;

            for (int i = 0; i < sessionItems.Count; i++) {
                string t = sessionItems[i].ToString();
            }

        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions) {
            locked = false;
            lockAge = new TimeSpan(1, 1, 1, 1, 1);
            lockId = null;
            actions = SessionStateActions.None;
            return null;
        }

        public override SessionStateStoreData GetItem(HttpContext context,
                  string id,
                  out bool locked,
                  out TimeSpan lockAge,
                  out object lockId,
                  out SessionStateActions actionFlags) {

            locked = false;
            lockAge = new TimeSpan(1, 1, 1, 1, 1);
            lockId = null;
            actionFlags = SessionStateActions.None;

                      return null;
        }

        private SessionStateStoreData GetSessionStoreItem(bool lockRecord,
          HttpContext context,
          string id,
          out bool locked,
          out TimeSpan lockAge,
          out object lockId,
          out SessionStateActions actionFlags) {

            // Initial values for return value and out parameters
            SessionStateStoreData item = null;
            lockAge = TimeSpan.Zero;
            lockId = null;
            locked = false;
            actionFlags = 0;


            // DateTime to check if current session item is expired.
            DateTime expires;
            // String to hold serialized SessionStateItemCollection.
            string serializedItems = "";
            // True if a record is found in the database.
            bool foundRecord = false;
            // True if the returned session item is expired and needs to be deleted.
            bool deleteData = false;
            // Timeout value from the data store.
            int timeout = 0;

            // lockRecord is true when called from GetItemExclusive and
            // false when called from GetItem.
            // Obtain a lock if possible. Ignore the record if it is expired.
            if (lockRecord) {

                /*
                query = Query.And(Query.EQ("_id", id), Query.EQ("ApplicationName", ApplicationName), Query.EQ("Locked", false), Query.GT("Expires", DateTime.Now.ToUniversalTime()));
                var update = Update.Set("Locked", true);
                update.Set("LockDate", DateTime.Now.ToUniversalTime());
                var result = sessionCollection.Update(query, update, _safeMode);

                if (result.DocumentsAffected == 0) {
                    // No record was updated because the record was locked or not found.
                    locked = true;
                }
                else {
                    // The record was updated.
                    locked = false;
                }
            }

            // Retrieve the current session item information.
            query = Query.And(Query.EQ("_id", id), Query.EQ("ApplicationName", ApplicationName));
            var results = sessionCollection.FindOneAs<BsonDocument>(query);

            if (results != null) {
                expires = results["Expires"].AsDateTime;

                if (expires < DateTime.Now.ToUniversalTime()) {
                    // The record was expired. Mark it as not locked.
                    locked = false;
                    // The session was expired. Mark the data for deletion.
                    deleteData = true;
                }
                else
                    foundRecord = true;

                serializedItems = results["SessionItems"].AsString;
                lockId = results["LockId"].AsInt32;
                lockAge = DateTime.Now.ToUniversalTime().Subtract(results["LockDate"].AsDateTime);
                actionFlags = (SessionStateActions)results["Flags"].AsInt32;
                timeout = results["Timeout"].AsInt32;
            }

            // If the returned session item is expired,
            // delete the record from the data source.
            if (deleteData) {
                query = Query.And(Query.EQ("_id", id), Query.EQ("ApplicationName", ApplicationName));
                sessionCollection.Remove(query, _safeMode);
            }

            // The record was not found. Ensure that locked is false.
            if (!foundRecord)
                locked = false;

            // If the record was found and you obtained a lock, then set
            // the lockId, clear the actionFlags,
            // and create the SessionStateStoreItem to return.
            if (foundRecord && !locked) {
                lockId = (int)lockId + 1;

                query = Query.And(Query.EQ("_id", id), Query.EQ("ApplicationName", ApplicationName));
                var update = Update.Set("LockId", (int)lockId);
                update.Set("Flags", 0);
                sessionCollection.Update(query, update, _safeMode);

                // If the actionFlags parameter is not InitializeItem,
                // deserialize the stored SessionStateItemCollection.
                if (actionFlags == SessionStateActions.InitializeItem)
                    item = CreateNewStoreData(context, (int)_config.Timeout.TotalMinutes);
                else
                    item = Deserialize(context, serializedItems, timeout);
            }
        }
        catch (Exception e) {
            if (WriteExceptionsToEventLog) {
                WriteToEventLog(e, "GetSessionStoreItem");
                throw new ProviderException(_exceptionMessage);
            }
            else
                throw e;
        }
        finally {
            conn.Disconnect();
        }
                 * */
            }

            locked = false;
            lockAge = new TimeSpan(1, 1, 1, 1, 1);
            lockId = null;
            actionFlags = SessionStateActions.None;
            return null;
        }


        private SessionStateStoreData Deserialize(HttpContext context,
        string serializedItems, int timeout) {
            using (MemoryStream ms =
              new MemoryStream(Convert.FromBase64String(serializedItems))) {

                SessionStateItemCollection sessionItems =
                  new SessionStateItemCollection();

                if (ms.Length > 0) {
                    using (BinaryReader reader = new BinaryReader(ms)) {
                        sessionItems = SessionStateItemCollection.Deserialize(reader);
                    }
                }

                return new SessionStateStoreData(sessionItems,
                  SessionStateUtility.GetSessionStaticObjects(context),
                  timeout);
            }
        }

        public override void ReleaseItemExclusive(HttpContext context,
                  string id,
                  object lockId) {
        }

        public override void RemoveItem(HttpContext context,
                  string id,
                  object lockId,
                  SessionStateStoreData item) {
        }

        public override void CreateUninitializedItem(HttpContext context,
                  string id,
                  int timeout) {

                      this.Repository.AddRedisItem(id, new KeyValuePair<string,SessionItem>(id, null));
        }

        public override SessionStateStoreData CreateNewStoreData(System.Web.HttpContext context, int timeout) {
            return new SessionStateStoreData(new SessionStateItemCollection(),
                SessionStateUtility.GetSessionStaticObjects(context),
                timeout);
        }

        public override void ResetItemTimeout(HttpContext context,
                                                      string id) {
        }

        public override void InitializeRequest(HttpContext context) {
        }

        public override void EndRequest(HttpContext context) {
            this.Dispose();
        }
    }
}