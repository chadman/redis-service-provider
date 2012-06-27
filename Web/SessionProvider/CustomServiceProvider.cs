using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Collections.Specialized;
using System.Web.SessionState;

namespace Web.SessionProvider {
    public class CustomServiceProvider : System.Web.SessionState.SessionStateStoreProviderBase, IDisposable {



        public override void Initialize(string name, NameValueCollection config) {
        }

        public override void Dispose() {
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback) {
            return true;
        }

        public override void SetAndReleaseItemExclusive(HttpContext context,
                  string id,
                  SessionStateStoreData item,
                  object lockId,
                  bool newItem) {
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

        public override SessionStateStoreData GetItemExclusive(HttpContext context,
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

                      locked = false;
                      lockAge = new TimeSpan(1, 1, 1, 1, 1);
                      lockId = null;
                      actionFlags = SessionStateActions.None;
                      return null;
        }

        private string Serialize(SessionStateItemCollection items) {

            return null;
        }

        private SessionStateStoreData Deserialize(HttpContext context,
                  string serializedItems, int timeout) {

            return null;
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
        }

        public override SessionStateStoreData CreateNewStoreData(
                  HttpContext context,
                  int timeout) {

                      return null;
        }

        public override void ResetItemTimeout(HttpContext context,
                                                      string id) {
        }

        public override void InitializeRequest(HttpContext context) {
        }

        public override void EndRequest(HttpContext context) {
        }

        

    }
}