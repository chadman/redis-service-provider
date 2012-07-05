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

namespace RedisProvider.SessionProvider {

    #region Repository
    public interface IRedisServiceProviderRepository {
        RedisClient OpenRedisClient();
    }

    public class RedisServiceProviderRepository : IRedisServiceProviderRepository {

        /// <summary>
        /// This will be used to open a redis connection with specific connection string information
        /// </summary>
        /// <returns></returns>
        public RedisClient OpenRedisClient() {
            return new RedisClient();
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
        public string SessionItems { get; set; }
        public int Flags { get; set; }
        #endregion Properties

    }
    #endregion Session Item Model

    public class CustomServiceProvider : System.Web.SessionState.SessionStateStoreProviderBase, IDisposable {

        #region Properties
        public IRedisServiceProviderRepository Repository { get; set; }

        private string ApplicationName {
            get {
                if (ConfigurationManager.AppSettings.AllKeys.Contains("Application.Name")) {
                    return ConfigurationManager.AppSettings["Application.Name"];
                }

                return string.Empty;
            }
        }
        private SessionStateSection sessionStateConfig = null;
        #endregion Properties

        #region Private Methods
        private string RedisKey(string id) {
            return string.Format("{0}{1}", !string.IsNullOrEmpty(this.ApplicationName) ? this.ApplicationName + ":" : "", id);
        }
        #endregion Private Methods
        

        #region Constructor
        public CustomServiceProvider() {
            this.Repository = new RedisServiceProviderRepository();
        }
        #endregion Constructor

        public override void Dispose() {
            
        }

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

            // Get <sessionState> configuration element.
            Configuration cfg = WebConfigurationManager.OpenWebConfiguration(ApplicationName);
            sessionStateConfig = (SessionStateSection)cfg.GetSection("system.web/sessionState");
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
            using (RedisClient client = this.Repository.OpenRedisClient()) {
                // Serialize the SessionStateItemCollection as a string.
                string sessionItems = Serialize((SessionStateItemCollection)item.Items);

                try {
                    if (newItem) {
                        SessionItem sessionItem = new SessionItem();
                        sessionItem.CreatedAt = DateTime.UtcNow;
                        sessionItem.LockDate = DateTime.UtcNow;
                        sessionItem.LockID = 0;
                        sessionItem.Timeout = item.Timeout;
                        sessionItem.Locked = false;
                        sessionItem.SessionItems = sessionItems;
                        sessionItem.Flags = 0;

                        client.Set<SessionItem>(this.RedisKey(id), sessionItem, DateTime.UtcNow.AddMinutes(item.Timeout));
                    }
                    else {
                        SessionItem currentSessionItem = client.Get<SessionItem>(this.RedisKey(id));
                        if (currentSessionItem != null && currentSessionItem.LockID == (int?)lockId) {
                            currentSessionItem.Locked = false;
                            currentSessionItem.SessionItems = sessionItems;
                            client.Set<SessionItem>(this.RedisKey(id), currentSessionItem, DateTime.UtcNow.AddMinutes(item.Timeout));
                        }
                    }
                }
                catch (Exception e) {
                    throw e;
                }
            }
        } 

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions) {
            return GetSessionStoreItem(true, context, id, out locked,  out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItem(HttpContext context,  string id,  out bool locked,  out TimeSpan lockAge, out object lockId, out SessionStateActions actionFlags) {
            return GetSessionStoreItem(false, context, id, out locked, out lockAge, out lockId, out actionFlags);
        }

        private SessionStateStoreData GetSessionStoreItem(bool lockRecord,
          HttpContext context,
          string id,
          out bool locked,
          out TimeSpan lockAge,
          out object lockId,
          out SessionStateActions actionFlags) {

            // Initial values for return value and out parameters.
            SessionStateStoreData item = null;
            lockAge = TimeSpan.Zero;
            lockId = null;
            locked = false;
            actionFlags = 0;

            // String to hold serialized SessionStateItemCollection.
            string serializedItems = "";

            // Timeout value from the data store.
            int timeout = 0;

            using (RedisClient client = this.Repository.OpenRedisClient()) {
                try {
                    if (lockRecord) {
                        locked = false;
                        SessionItem currentItem = client.Get<SessionItem>(this.RedisKey(id));

                        if (currentItem != null) {
                            // If the item is locked then do not attempt to update it
                            if (!currentItem.Locked) {
                                currentItem.Locked = true;
                                currentItem.LockDate = DateTime.UtcNow;
                                client.Set<SessionItem>(this.RedisKey(id), currentItem, DateTime.UtcNow.AddMinutes(sessionStateConfig.Timeout.TotalMinutes));
                            }
                            else {
                                locked = true;
                            }
                        }
                    }

                    SessionItem currentSessionItem = client.Get<SessionItem>(this.RedisKey(id));

                    if (currentSessionItem != null) {
                        serializedItems = currentSessionItem.SessionItems;
                        lockId = currentSessionItem.LockID;
                        lockAge = DateTime.UtcNow.Subtract(currentSessionItem.LockDate);
                        actionFlags = (SessionStateActions)currentSessionItem.Flags;
                        timeout = currentSessionItem.Timeout;
                    }
                    else {
                        locked = false;
                    }

                    if (currentSessionItem != null && !locked) {
                        // Delete the old item before inserting the new one
                        client.Remove(this.RedisKey(id));

                        lockId = (int?)lockId + 1;
                        currentSessionItem.LockID = lockId != null ? (int)lockId : 0;
                        currentSessionItem.Flags = 0;

                        client.Set<SessionItem>(this.RedisKey(id), currentSessionItem, DateTime.UtcNow.AddMinutes(sessionStateConfig.Timeout.TotalMinutes));

                        // If the actionFlags parameter is not InitializeItem,
                        // deserialize the stored SessionStateItemCollection.
                        if (actionFlags == SessionStateActions.InitializeItem) {
                            item = CreateNewStoreData(context, 30);
                        }
                        else {
                            item = Deserialize(context, serializedItems, timeout);
                        }
                    }
                }

                catch (Exception e) {
                    throw e;
                }
            }

            return item;
        }


        private SessionStateStoreData Deserialize(HttpContext context,  string serializedItems, int timeout) {
            using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(serializedItems))) {
                SessionStateItemCollection sessionItems = new SessionStateItemCollection();

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

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId) {

            using (RedisClient client = this.Repository.OpenRedisClient()) {
                SessionItem currentSessionItem = client.Get<SessionItem>(this.RedisKey(id));

                if (currentSessionItem != null && (int?)lockId == currentSessionItem.LockID) {
                    currentSessionItem.Locked = false;
                    client.Set<SessionItem>(this.RedisKey(id), currentSessionItem, DateTime.UtcNow.AddMinutes(sessionStateConfig.Timeout.TotalMinutes));
                }
            }
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item) {
            using (RedisClient client = this.Repository.OpenRedisClient()) {
                // Delete the old item before inserting the new one
                client.Remove(this.RedisKey(id));
            }
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout) {
            using (RedisClient client = this.Repository.OpenRedisClient()) {
                SessionItem sessionItem = new SessionItem();
                sessionItem.CreatedAt = DateTime.Now.ToUniversalTime();
                sessionItem.LockDate = DateTime.Now.ToUniversalTime();
                sessionItem.LockID = 0;
                sessionItem.Timeout = timeout;
                sessionItem.Locked = false;
                sessionItem.SessionItems = string.Empty;
                sessionItem.Flags = 0;

                client.Set<SessionItem>(this.RedisKey(id), sessionItem, DateTime.UtcNow.AddMinutes(timeout));
            }
        }

        public override SessionStateStoreData CreateNewStoreData(System.Web.HttpContext context, int timeout) {
            return new SessionStateStoreData(new SessionStateItemCollection(),
                SessionStateUtility.GetSessionStaticObjects(context),
                timeout);
        }

        public override void ResetItemTimeout(HttpContext context,  string id) {

            using (RedisClient client = this.Repository.OpenRedisClient()) {
                try {
                    // TODO :: GET THIS VALUE FROM THE CONFIG
                    client.ExpireEntryAt(id, DateTime.UtcNow.AddMinutes(sessionStateConfig.Timeout.TotalMinutes));
                }
                catch (Exception e) {
                    throw e;
                }
            }
        }

        public override void InitializeRequest(HttpContext context) {
            // Was going to open the redis connection here but sometimes I had 5 connections open at one time which was strange
        }

        public override void EndRequest(HttpContext context) {
            this.Dispose();
        }
    }
}