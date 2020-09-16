using log4net;
using MongoDB.Bson;
using MongoDB.Driver;
using ProjectCommon.Unit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{
    /// <summary>
    /// mongo数据库
    /// </summary>
    /// <typeparam name="TMongoDB"></typeparam>
    public abstract class MongoHelper<TMongoDB> : 
        SingleInstance<TMongoDB> where TMongoDB : class, new()
    {
        public MongoHelper()
        {
            _InsertList = new Dictionary<string, List<BsonDocument>>();
        }

        protected ILog _log { get; set; }

        protected MongoClient _client { get; set; }

        //数据库操作缓存队列
        Dictionary<string, List<BsonDocument>> _InsertList { get; set; }


        protected virtual string MongosConfig => "mongos";
        protected virtual string MongoConfig => "mongo";

        public virtual void Init(string host, int port, string dbName = "", string userName = "", string password = "", ILog log = null)
        {
            _log = log;

            MongoClientSettings mcs = new MongoClientSettings();
            mcs.Server = new MongoServerAddress(host, port);

            if (!string.IsNullOrEmpty(password))
            {
                // mcs.Credentials = new[] { MongoCredential.CreateCredential(dbName, userName, password) };
                mcs.Credential = MongoCredential.CreateCredential(dbName, userName, password);
                mcs.SslSettings = new SslSettings { CheckCertificateRevocation = false };
            }

            _client = new MongoClient(mcs);

            MDB = _client.GetDatabase(dbName);
            InitTable();
        }

        public virtual void Init(IMongoConfig mongoConfig, ILog log = null)
        {
            MongoClientSettings mcs = new MongoClientSettings();
            mcs.Server = new MongoServerAddress(mongoConfig.Host, mongoConfig.Port);

            if (mongoConfig.Auth)
            {
                string pwd = mongoConfig.AuthPassword;
                //if(cfg.Encrypt)
                //{
                //    byte[] bs = Convert.FromBase64String(pwd);
                //    pwd = Encoding.Default.GetString(bs);
                //}

                mcs.Credential = MongoCredential.CreateCredential(mongoConfig.AuthDBName, mongoConfig.AuthUser, CreateSecureString(pwd));
                mcs.SslSettings = new SslSettings { CheckCertificateRevocation = false };
            }            

            _client = new MongoClient(mcs);

            MDB = _client.GetDatabase(mongoConfig.DBName);
            InitTable();
        }

        protected static SecureString CreateSecureString(string str)
        {
            if (str != null)
            {
                var secureStr = new SecureString();
                foreach (var c in str)
                {
                    secureStr.AppendChar(c);
                }
                secureStr.MakeReadOnly();
                return secureStr;
            }

            return null;
        }

        /// <summary>
        /// 数据库
        /// </summary>
        protected IMongoDatabase MDB { get; set; }


        #region IsExists & ExecGetCount

        /// <summary>
        /// 是否存在 (field)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected async Task<bool> IsExists(string table, string field, object value)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(field, BsonValue.Create( value));
            return await IsExists(table, filter);
        }

        /// <summary>
        /// 是否存在(filter)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        protected async Task<bool> IsExists(string table, FilterDefinition<BsonDocument> filter)
        {
            var count = await ExecGetCount(table, filter);
            return count > 0;
        }

        /// <summary>
        /// 查询数据数
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        protected async Task<long> ExecGetCount(string table)
        {
            return await ExecGetCount(table, null);
        }

        /// <summary>
        /// 查询数据数(filter)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        protected async Task<long> ExecGetCount(string table, FilterDefinition<BsonDocument> filter = null)
        {
            try
            {
                var collection = MDB.GetCollection<BsonDocument>(table);

                if (filter == null)
                {
                    var doc = new BsonDocument();
                    return await collection.CountDocumentsAsync(doc);
                }
                else
                {
                    return await collection.CountDocumentsAsync(filter);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
            }
            return 0;
        }
        #endregion

        # region ExecUpdate
        /// <summary>
        /// 更新数据(_id)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="objId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected async Task<bool> ExecUpdate(string table, ObjectId objId, BsonDocument data, bool mutli = false)
        {
            return await ExecUpdate(table, "_id", objId, data, mutli);
        }

        public virtual async Task UpdateField<T>(string table, FieldBase findFiled, FieldBase updateField)
            where T : IMongoObject
        {
            var filter = Builders<T>.Filter.Eq(findFiled.FieldName, findFiled.GetObject());
            var update = Builders<T>.Update.Set(updateField.GetFullFieldPath(), updateField.GetBsonValue(true));

            var c = this.MDB.GetCollection<T>(table);
            await c.UpdateOneAsync(filter, update);
            return;
        }

        /// <summary>
        /// 更新数据(field)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="field"></param>
        /// <param name="val"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected async Task<bool> ExecUpdate(string table, string field, object value, BsonDocument data, bool mutli = false)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(field, BsonValue.Create(value));
            return await ExecUpdate(table, filter, data, mutli);
        }

        /// <summary>
        /// 更新数据(filter)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="filter"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected async Task<bool> ExecUpdate(string table, FilterDefinition<BsonDocument> filter, BsonDocument data, bool mutli = false)
        {
            try
            {
                var collection = MDB.GetCollection<BsonDocument>(table);

                var list = data.GetEnumerator();
                bool ret = list.MoveNext();
                if (!ret)
                    return false;

                var update = Builders<BsonDocument>.Update.Set(list.Current.Name, list.Current.Value);

                while (list.MoveNext())
                {
                    update = update.Set(list.Current.Name, list.Current.Value);
                }

                UpdateOptions options = new UpdateOptions() { IsUpsert = true };
                // _log.Debug($"Save document = {data.ToString()}");

                if(mutli)
                {
                    var retu = await collection.UpdateManyAsync(filter, update, options);
                    return retu.IsModifiedCountAvailable;
                }
                else
                {
                    var retu = await collection.UpdateOneAsync(filter, update, options);
                    return retu.IsModifiedCountAvailable;
                }
                
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
            return false;
        }

        /// <summary>
        /// 修改多条数据(field)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="field"></param>
        /// <param name="filter"></param>
        /// <param name="datalist"></param>
        /// <returns></returns>
        protected async Task<bool> ExecUpdate(string table, string field, FilterDefinition<BsonDocument> filter, List<BsonDocument> datalist)
        {
            if (datalist == null)
                return false;

            try
            {
                foreach (var it in datalist)
                {
                    var endfilter = filter & Builders<BsonDocument>.Filter.Eq(field, it[field]);

                    var ret = await ExecUpdate(table, endfilter, it);
                    if (!ret)
                        return ret;
                }
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
            return false;
        }
        #endregion

        #region ExecInc
        /// <summary>
        /// 字段增减(_key)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="val"></param>
        /// <param name="upcount"></param>
        /// <returns></returns>
        protected Task<BsonDocument> ExecInc(string table, object value, long upcount = 1)
        {
            return ExecInc(table, "Key", value, "Count", upcount);
        }

        /// <summary>
        /// 字段增减(anykey)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <param name="field"></param>
        /// <param name="upcount"></param>
        /// <returns></returns>
        protected async Task<BsonDocument> ExecInc(string table, string key, object value, string field = "Count", long upcount = 1)
        {
            try
            {
                var collection = MDB.GetCollection<BsonDocument>(table);
                var filter = Builders<BsonDocument>.Filter.Eq(key, BsonValue.Create(value));
                var update = Builders<BsonDocument>.Update.Inc(field, upcount);

                var ret = await collection.FindOneAndUpdateAsync(filter, update);
                return ret;
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
                throw ex;
            }
        }

        protected async Task<BsonDocument> ExecInc(string table, FilterDefinition<BsonDocument> filter, string field, long upcount = 1)
        {
            try
            {
                var collection = MDB.GetCollection<BsonDocument>(table);
                var update = Builders<BsonDocument>.Update.Inc(field, upcount);

                var ret = await collection.FindOneAndUpdateAsync(filter, update);
                return ret;
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
                throw ex;
            }
        }


        #endregion

        #region ExecRemove
        /// <summary>
        /// 删除数据(_id)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="objId"></param>
        protected Task ExecRemove(string table, ObjectId objId, bool mutli = false)
        {
            return ExecRemove(table, "_id", objId, mutli);
        }

        /// <summary>
        /// 删除数据(key)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        protected Task ExecRemove(string table, string key, object value, bool mutli = false)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(key, BsonValue.Create( value));
            return ExecRemove(table, filter, mutli);
        }

        /// <summary>
        /// 删除数据(filter)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="filter"></param>
        protected async Task ExecRemove(string table, FilterDefinition<BsonDocument> filter, bool mutli = false)
        {
            try
            {
                var collection = MDB.GetCollection<BsonDocument>(table);
                if(mutli)
                {
                    await collection.DeleteManyAsync(filter);
                }
                else
                {
                    await collection.DeleteOneAsync(filter);
                }
                
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// 删除表
        /// </summary>
        /// <param name="table"></param>
        protected async Task ExecRemoveAll(string table)
        {
            try
            {
                await MDB.DropCollectionAsync(table);
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
            }
        }

        protected Task<bool> ExecRemoveKey(string table, string key, object value, string removeKey)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(key, BsonValue.Create(value));
            return ExecRemoveKey(table, filter, new List<string>() { removeKey });
        }

        protected Task<bool> ExecRemoveKey(string table, string key, object value, List<string> removeKeys)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(key, BsonValue.Create(value));
            return ExecRemoveKey(table, filter, removeKeys);
        }

        /// <summary>
        /// 删除指定key
        /// </summary>
        /// <param name="table"></param>
        /// <param name="filter"></param>
        /// <param name="keys"></param>
        /// <returns></returns>
        protected async Task<bool> ExecRemoveKey(string table, FilterDefinition<BsonDocument> filter, List<string> keys)
        {
            try
            {
                UpdateDefinition<BsonDocument> doc = null;
                foreach (var key in keys)
                {
                    if (doc == null)
                    {
                        doc = Builders<BsonDocument>.Update.Unset(key);
                    }
                    else
                    {
                        doc = doc.Unset(key);
                    }
                }

                var collection = MDB.GetCollection<BsonDocument>(table);
                var ret = await collection.UpdateOneAsync(filter, doc);
                return true;
            }
            catch(Exception ex)
            {
                _log.Error(ex.Message, ex);
                return false;
            }
        }

        #endregion

        #region ExecGetOne & ExecGetList
        /// <summary>
        /// 获取数据(_id)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="objId"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        protected async Task<BsonDocument> ExecGetOne(string table, ObjectId objId, string[] fields = null)
        {
            return await ExecGetOne(table, "_id", objId, fields);
        }

        /// <summary>
        /// 获取数据(key)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        protected async Task<BsonDocument> ExecGetOne(string table, string key, object value, string[] fields = null)
        {
            var filter = Builders<BsonDocument>.Filter.Eq(key, BsonValue.Create( value));
            return await ExecGetOne(table, filter, fields);
        }

        /// <summary>
        /// 获取数据(filter)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="filter"></param>
        /// <param name="fields"></param>
        /// <returns></returns>
        protected async Task<BsonDocument> ExecGetOne(string table, FilterDefinition<BsonDocument> filter, string[] fields = null)
        {
            BsonDocument retval = null;
            try
            {
                var list = await ExecGetList(table, filter, fields, "", true, 0, 1, true);
                if (list.Count == 1)
                    retval = list[0];

                return retval;
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
            return retval;
        }

        protected Task<T> ExecGetOne<T>(string table, FieldBase field, string[] fields = null) where T : MongoObject<T>, new()
        {
            var filter = Builders<T>.Filter.Eq(field.FieldName, field.GetObject());
            return ExecGetOne<T>(table, filter, fields);
        }

        protected async Task<T> ExecGetOne<T>(string table, FilterDefinition<T> filter, string[] fields = null) where T : MongoObject<T>, new()
        {
            var collection = MDB.GetCollection<T>(table);
            FindOptions<T, T> opt = new FindOptions<T, T>();
            opt.Limit = 1;
            if (fields != null && fields.Length > 0)
            {
                var proj = Builders<T>.Projection.Include(fields[0]);
                for (int i = 1; i < fields.Length; i++)
                {
                    proj = proj.Include(fields[i]);
                }
                opt.Projection = proj;
            }
            var datas = await collection.FindAsync(filter, opt);
            while(await datas.MoveNextAsync())
            {
                return datas.First();
            }

            return null;
        }

        ///// <summary>
        ///// 获取数据列表(key)
        ///// </summary>
        ///// <param name="table"></param>
        ///// <param name="key"></param>
        ///// <param name="val"></param>
        ///// <param name="fields">返回字段</param>
        ///// <param name="sort">排序字段</param>
        ///// <param name="asc">升序</param>
        ///// <param name="skip">跳过行数</param>
        ///// <param name="limit">截取行数</param>
        ///// <returns></returns>
        //public Task<List<BsonDocument>> ExecGetList(string table, string key, object value, string[] fields = null,
        //    string sort = "", bool asc = true, int skip = 0, int limit = 0)
        //{
        //    var filter = Builders<BsonDocument>.Filter.Eq(key, BsonValue.Create( value));
        //    return ExecGetList(table, filter, fields, sort, asc, skip, limit);
        //}

        protected Task<IAsyncCursor<T>> ExecGetList<T>(string table, FilterDefinition<T> filter, string[] fields = null) where T : MongoObject<T>, new()
        {
            var collection = MDB.GetCollection<T>(table);
            FindOptions<T, T> opt = new FindOptions<T, T>();
            opt.Limit = 1;
            if (fields != null && fields.Length > 0)
            {
                var proj = Builders<T>.Projection.Include(fields[0]);
                for (int i = 1; i < fields.Length; i++)
                {
                    proj = proj.Include(fields[i]);
                }
                opt.Projection = proj;
            }
            return collection.FindAsync(filter, opt);
        }

        /// <summary>
        /// 获取数据列表(filter)
        /// </summary>
        /// <param name="table"></param>
        /// <param name="filter"></param>
        /// <param name="fields">返回字段</param>
        /// <param name="sort">排序字段</param>
        /// <param name="asc">升序</param>
        /// <param name="skip">跳过行数</param>
        /// <param name="limit">截取行数</param>
        /// <returns></returns>
        protected async Task<List<BsonDocument>> ExecGetList(string table, FilterDefinition<BsonDocument> filter, string[] fields = null,
            string sort = "", bool asc = true, int skip = 0, int limit = 0, bool bsondoc = true, string[] excludeFields = null)
        {
            List<BsonDocument> retlist = new List<BsonDocument>();
            try
            {
                var collection = MDB.GetCollection<BsonDocument>(table);

                FindOptions<BsonDocument, BsonDocument> fo = new FindOptions<BsonDocument, BsonDocument>();

                if(limit > 0)
                    fo.Limit = limit;

                if(skip > 0)
                    fo.Skip = skip;

                if(sort != "")
                {
                    if(asc)
                        fo.Sort = Builders<BsonDocument>.Sort.Ascending(sort);
                    else
                        fo.Sort = Builders<BsonDocument>.Sort.Descending(sort);
                }

                if(fields != null && fields.Length > 0)
                {
                    var proj = Builders<BsonDocument>.Projection.Include(fields[0]);
                    for(int i =1; i<fields.Length; i++)
                    {
                        proj = proj.Include(fields[i]);
                    }

                    fo.Projection = proj;
                }

                if (excludeFields != null && excludeFields.Length > 0)
                {
                    var proj = Builders<BsonDocument>.Projection.Exclude(excludeFields[0]);
                    for (int i = 1; i < excludeFields.Length; i++)
                    {
                        proj = proj.Exclude(excludeFields[i]);
                    }

                    fo.Projection = proj;
                }
               

                using (var cursor = await collection.FindAsync(filter, fo))
                {
                    while (await cursor.MoveNextAsync())
                    {
                        var batch = cursor.Current;
                        foreach (var doc in batch)
                        {
                            retlist.Add(doc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
                retlist.Clear();
            }
            return retlist;
        }
        #endregion

        #region ExecInsert
        /// <summary>
        /// 插入数据
        /// </summary>
        /// <param name="table"></param>
        /// <param name="data"></param>
        /// <param name="delay">是否延迟插入(队列)</param>
        protected async Task ExecInsert(string table, BsonDocument data, bool delay = false)
        {
            if (!delay)  //立即入库
            {
                try
                {
                    var collection = MDB.GetCollection<BsonDocument>(table);
                    await collection.InsertOneAsync(data);
                }
                catch (Exception ex)
                {
                    _log.Error(ex.ToString());
                }
            }
            else //加入队列
            {
                List<BsonDocument> listDocument = null;

                if (_InsertList.ContainsKey(table))
                {
                    listDocument = _InsertList[table];
                }
                else
                {
                    listDocument = new List<BsonDocument>();
                }
                listDocument.Add(data.ToBsonDocument());
                _InsertList[table] = listDocument;
            }
        }

        /// <summary>
        /// 插入多条数据
        /// </summary>
        /// <param name="table"></param>
        /// <param name="datalist"></param>
        protected async void ExecInsert(string table, List<BsonDocument> datalist)
        {
            if (datalist == null || datalist.Count == 0)
                return;

            try
            {

                var collection = MDB.GetCollection<BsonDocument>(table);

                await collection.InsertManyAsync(datalist);
               
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
            }
        }
        
        ///// <summary>
        ///// 插入缓存数据
        ///// </summary>
        //public void ExecInsert()
        //{
        //    foreach (var item in _InsertList)
        //    {
        //        ExecInsert(item.Key, item.Value);
        //    }

        //    //CLEAR
        //    _InsertList.Clear();
        //}
        #endregion

        /// <summary>
        /// 初始化表索引
        /// </summary>
        protected abstract void InitTable();
    }


    public static class BsonDocumentExtension
    {
        public static T ParseBsonDocument<T>(this BsonDocument doc) where T : MongoObject, new()
        {
            if (doc == null)
            {
                return null;
            }

            T db = new T();
            db.ParseBsonValue(doc);

            return db;
        }

        public static List<T> ParseBsonDocument<T>(this IEnumerable<BsonDocument> docs) where T : MongoObject, new()
        {
            if (docs == null || docs.Count() <= 0)
            {
                return new List<T>();
            }

            List<T> results = new List<T>();
            foreach(var doc in docs)
            {
                T db = new T();
                db.ParseBsonValue(doc);
                results.Add(db);
            }

            return results;
        }
    }
}
