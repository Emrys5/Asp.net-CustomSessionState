using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.SessionState;

namespace CustomSessionState
{

    /// <summary>
    /// 自定义Session实现方式
    /// </summary>
    public class MyCustomSessionStateStoreProvider : SessionStateStoreProviderBase
    { 
        /// <summary>
        /// 获取配置文件的设置的默认超时时间
        /// </summary>
        private static TimeSpan _expiresTime; 

        /// <summary>
        /// 获取Web.config 在sessionState设置的超时时间
        /// </summary>
        static MyCustomSessionStateStoreProvider()
        {
            System.Web.Configuration.SessionStateSection sessionStateSection = (System.Web.Configuration.SessionStateSection)System.Configuration.ConfigurationManager.GetSection("system.web/sessionState");
            _expiresTime = sessionStateSection.Timeout;
        }

        /// <summary>
        /// 创建新的存储数据
        /// </summary>
        /// <param name="context"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        /// <summary>
        /// 创建未初始化的项，就是初始化Session数据
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <param name="timeout"></param>
        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            using (SessionStateEF db = new SessionStateEF())
            {
                var session = new ASPStateTempSessions
                {
                    Created = DateTime.Now,
                    Expires = DateTime.Now.AddMinutes(timeout),
                    Flags = (int)SessionStateActions.InitializeItem,
                    LockDate = DateTime.Now,
                    Locked = false,
                    SessionId = id,
                    LockId = 0,
                    Timeout = timeout
                };
                db.ASPStateTempSessions.Add(session);
                db.SaveChanges();
            }
        }

        /// <summary>
        /// 释放锁定的项，就是把锁定的Session的锁的状态清除掉
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <param name="lockId"></param>
        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            using (SessionStateEF db = new SessionStateEF())
            {
                var session = db.ASPStateTempSessions.Find(id);
                if (session == null)
                {
                    return;
                }

                // 把locked设置为false
                session.Locked = false;
                session.Expires = DateTime.Now + _expiresTime;
                db.SaveChanges();
            }

        }

        /// <summary>
        /// 删除Session，会在Session.Abandon()的时候调用
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <param name="lockId"></param>
        /// <param name="item"></param>
        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            using (SessionStateEF db = new SessionStateEF())
            {
                var session = db.ASPStateTempSessions.Find(id);
                if (session == null)
                {
                    return;
                }

                db.ASPStateTempSessions.Remove(session);
                db.SaveChanges();
            }
        }

        /// <summary>
        /// 设置超时时间
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        public override void ResetItemTimeout(HttpContext context, string id)
        {
            using (SessionStateEF db = new SessionStateEF())
            {
                var session = db.ASPStateTempSessions.Find(id);
                if (session == null)
                {
                    return;
                }
                session.Expires = DateTime.Now + _expiresTime;
                db.SaveChanges();
            }
        }

        /// <summary>
        /// 新建或者释放锁定的项并设置Session的值
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <param name="item"></param>
        /// <param name="lockId"></param>
        /// <param name="newItem"></param>
        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            using (SessionStateEF db = new SessionStateEF())
            {
                // 判断是否是新建，如果是新建则和CreateUninitializedItem不同在于Timeout和有初始值。
                if (newItem)
                {
                    var session = new ASPStateTempSessions
                    {
                        Created = DateTime.Now,
                        Expires = DateTime.Now.AddMinutes(item.Timeout),
                        Flags = (int)SessionStateActions.None,
                        LockDate = DateTime.Now,
                        Locked = false,
                        SessionId = id,
                        LockId = 0,
                        Timeout = item.Timeout,
                        SessionItem = Serialize((SessionStateItemCollection)item.Items)
                    };
                    db.ASPStateTempSessions.Add(session);
                    db.SaveChanges();
                }
                else// 释放锁定的项并设置Session的值
                {
                    var session = db.ASPStateTempSessions.FirstOrDefault(i => i.SessionId == id);
                    if (session == null)
                    {
                        return;
                    }

                    session.Expires = DateTime.Now.AddMinutes(item.Timeout);
                    session.Locked = false;
                    session.LockId = Convert.ToInt32(lockId);
                    session.SessionItem = Serialize((SessionStateItemCollection)item.Items);
                    db.SaveChanges();
                }
            }
        }

        /// <summary>
        /// 获取项，这个方式主要是把Session状态设置为只读状态时调用。
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <param name="locked"></param>
        /// <param name="lockAge"></param>
        /// <param name="lockId"></param>
        /// <param name="actions"></param>
        /// <returns></returns>
        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return DoGet(false, context, id, out locked, out lockAge, out lockId, out actions);
        }

        /// <summary>
        /// 独占获取项，除了Session状态为只读时调用
        /// </summary>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <param name="locked"></param>
        /// <param name="lockAge"></param>
        /// <param name="lockId"></param>
        /// <param name="actions"></param>
        /// <returns></returns>
        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            return DoGet(true, context, id, out locked, out lockAge, out lockId, out actions);
        }

        /// <summary>
        /// 获取Session的值
        /// </summary>
        /// <param name="isExclusive"></param>
        /// <param name="context"></param>
        /// <param name="id"></param>
        /// <param name="locked"></param>
        /// <param name="lockAge"></param>
        /// <param name="lockId"></param>
        /// <param name="actions"></param>
        /// <returns></returns>
        public SessionStateStoreData DoGet(bool isExclusive, HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            using (SessionStateEF db = new SessionStateEF())
            {
                // 设置初始值
                var item = default(SessionStateStoreData);
                lockAge = TimeSpan.Zero;
                lockId = null;
                locked = false;
                actions = 0;

                // 如果数据存储区中未找到任何会话项数据，则GetItemExclusive 方法将 locked 输出参数设置为false，并返回 null。
                // 这将导致 SessionStateModule调用 CreateNewStoreData 方法来为请求创建一个新的SessionStateStoreData 对象。
                var session = db.ASPStateTempSessions.Find(id);
                if (session == null)
                {
                    return null;
                }

                // 判断session是否是ReadOnly 模式，不是readonly模式得判断是否锁住
                if (isExclusive)
                {
                    // 如果在数据存储区中找到会话项数据但该数据已锁定，则GetItemExclusive 方法将 locked 输出参数设置为true，
                    // 将 lockAge 输出参数设置为当前日期和时间与该项锁定日期和时间的差，将 lockId 输出参数设置为从数据存储区中检索的锁定标识符，并返回 nul
                    if (session.Locked)
                    {
                        locked = true;
                        lockAge = session.LockDate - DateTime.Now;
                        lockId = session.LockId;
                        return null;
                    }
                }

                // 判断是否过期
                if (session.Expires < DateTime.Now)
                {
                    db.ASPStateTempSessions.Remove(session);
                    return null;
                }

                // 处理值
                lockId = lockId == null ? 0 : (int)lockId + 1;
                session.Flags = (int)SessionStateActions.None;
                session.LockId = Convert.ToInt32(lockId);

                // 获取timeout
                var timeout = actions == SessionStateActions.InitializeItem ? _expiresTime.TotalMinutes : session.Timeout;

                // 获取SessionStateItemCollection 
                SessionStateItemCollection sessionStateItemCollection = null;

                // 获取Session的值
                if (actions == SessionStateActions.None && !string.IsNullOrEmpty(session.SessionItem))
                {
                    sessionStateItemCollection = Deserialize((session.SessionItem));
                }

                item = new SessionStateStoreData(sessionStateItemCollection ?? new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), (int)timeout);

                return item;

            }

        }


        #region 序列化反序列化Session的值 
        /// <summary>
        /// 反序列化Session的数据
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public SessionStateItemCollection Deserialize(string item)
        {
            MemoryStream stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(item));
            SessionStateItemCollection collection = new SessionStateItemCollection();
            if (stream.Length > 0)
            {
                BinaryReader reader = new BinaryReader(stream);
                collection = SessionStateItemCollection.Deserialize(reader);
            }
            return collection;
        } 

        /// <summary>
        /// 序列化Session的数据
        /// </summary>
        /// <param name="items"></param>
        /// <returns></returns>
        public string Serialize(SessionStateItemCollection items)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            if (items != null)
                items.Serialize(writer);
            writer.Close();
            return System.Text.Encoding.ASCII.GetString(ms.ToArray());
        }

        #endregion 


        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return true;
        }
        public override void InitializeRequest(HttpContext context)
        {
        } 
        public override void EndRequest(HttpContext context)
        {
        } 
        public override void Dispose()
        {
        }

    }
}