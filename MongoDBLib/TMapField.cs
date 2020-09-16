using MongoDB.Bson;
using MongoDB.Bson.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{

    public class TMapField<Key, TMongoObject> : FieldBase, IEnumerable<KeyValuePair<Key, TMongoObject>>
        where TMongoObject : MongoObject, new()
        where Key : IConvertible
    {
        Dictionary<Key, TMongoObject> _value { get; set; }

        string KeyName { get; set; }

        /// <summary>
        /// 记录发生改变了的key，
        /// 如果某个key里面的数据发生了改变，
        /// 那么在更新的时候需要更新整个key的值
        /// </summary>
        List<Key> _ChangedKey { get; set; }

        public TMapField(eFieldType type, string name, IMongoObject parent)
            : base(type, name, parent)
        {
            var kval = typeof(Key);
            KeyName = kval.Name;
            _value = new Dictionary<Key, TMongoObject>();
            _ChangedKey = new List<Key>();
        }

        public TMongoObject GetValue(Key key)
        {
            if (_value.ContainsKey(key))
            {
                return _value[key];
            }
            return default(TMongoObject);
        }

        public TMongoObject FindOne(Func<TMongoObject, bool> selector)
        {
            foreach (var kv in _value)
            {
                if (selector(kv.Value))
                {
                    return kv.Value;
                }
            }

            return default(TMongoObject);
        }

        public List<TMongoObject> FindObject(Func<TMongoObject, bool> selector)
        {
            List<TMongoObject> objFind = new List<TMongoObject>();
            foreach (var kv in _value)
            {
                if (selector(kv.Value))
                {
                    objFind.Add(kv.Value);
                }
            }
            return objFind;
        }

        public List<TMongoObject> FindObject(Func<Key, TMongoObject, bool> selector)
        {
            List<TMongoObject> objFind = new List<TMongoObject>();
            foreach (var kv in _value)
            {
                if (selector(kv.Key, kv.Value))
                {
                    objFind.Add(kv.Value);
                }
            }
            return objFind;
        }


        /// <summary>
        /// 获取值
        /// </summary>
        /// <returns></returns>
        public Dictionary<Key, TMongoObject> GetValue()
        {
            return _value;
        }

        public TMongoObject this[Key key]
        {
            get
            {
                return GetValue(key);
            }
        }

        public bool ContainsKey(Key key)
        {
            return _value.ContainsKey(key);
        }

        public IEnumerable<Key> Keys
        {
            get
            {
                return _value.Keys;
            }
        }

        public int Count
        {
            get
            {
                return _value.Count;
            }
        }

        public bool Remove(Key key)
        {
            _parent?.UnsetKey(this.FieldName + key.ToString());
            return _value.Remove(key);
        }

        public override void UnsetKey(string key, IMongoObject self)
        {
            TMongoObject obj = self as TMongoObject;
            if (obj == null)
            {
                return;
            }

            foreach(var kv in _value)
            {
                if (kv.Value == obj)
                {
                    _parent.UnsetKey(this.FieldName + "." + kv.Key.ToString() + "." + key);
                    return;
                }
            }
        }

        public KeyValuePair<Key, TMongoObject> FirstOrDefault()
        {
            return _value.FirstOrDefault();
        }

        //public Dictionary<Key, T> Value
        //{
        //    get
        //    {
        //        return GetValue();
        //    }
        //}

        public void AddValue(Key key, TMongoObject val, object param = null, bool isUpdate = true)
        {
            val.SetParent(this);
            _value.Add(key, val);
            SetUpdate(isUpdate, this);
            FuncFieldChange?.Invoke(this, param);
        }

        /// <summary>
        /// 设置值
        /// </summary>
        /// <param name="val"></param>
        /// <param name="param"></param>
        /// <param name="isUpdate"></param>
        public virtual void SetValue(Dictionary<Key, TMongoObject> val, object param = null, bool isUpdate = true)
        {
            foreach(var kv in val)
            {
                kv.Value.SetParent(this);
            }

            _value = val;
            SetUpdate(isUpdate, this);
            FuncFieldChange?.Invoke(this, param);
        }

        public void SetValue(Key key, TMongoObject value, object param = null, bool isUpdate = true)
        {
            value.SetParent(this);
            _value[key] = value;
            SetUpdate(isUpdate, this);
            FuncFieldChange?.Invoke(this, param);
        }

        public override BsonValue GetBsonValue(bool all = false)
        {
            BsonDocument tmpDic = new BsonDocument();
            foreach (var kv in _value)
            {
                tmpDic.Add(new BsonElement(kv.Key.ToString(), kv.Value.GetBsonValue(all)));
            }

            return tmpDic;
        }

        public override BsonValue GetBsonValue(bool all, ref bool hasUpdateValue, ref string fieldKey)
        {
            BsonDocument tmpDic = new BsonDocument();
            hasUpdateValue = false;
            foreach (var kv in _value)
            {
                if (all)
                {
                    // 更新全部节点
                    var hasSubUpdateValue = false;
                    var valueBson = kv.Value.GetBsonValue(all, ref hasSubUpdateValue);
                    tmpDic.Add(kv.Key.ToString(), valueBson);
                    hasUpdateValue = hasSubUpdateValue;
                }
                else
                {
                    // 更新部分节点
                    foreach(var key in _ChangedKey)
                    {
                        // 由于这里采用增量更新，所以子节点必须全部更新才行，否则会引起数据丢失
                        var valueBson = _value[key].GetBsonValue(true);
                        tmpDic.Add(this.FieldName + "." + key, valueBson);
                        /*tmpDic.Set(key.ToString(), valueBson);*/
                        hasUpdateValue = true;
                    }
                }

                _ChangedKey.Clear();
                SetUpdate(false, this);
            }

            return tmpDic;
        }

        public override void ParseBsonValue(BsonValue val, MongoObject parent)
        {
            if (val.IsBsonNull)
            {
                return;
            }

            var bsonDic = val as BsonDocument;
            foreach (var bson in bsonDic)
            {
                try
                {
                    var key = (Key)Convert.ChangeType(bson.Name, typeof(Key));
                    TMongoObject subNode = new TMongoObject();
                    var doc = bson.Value as BsonDocument;
                    subNode.ParseBsonValue(doc);
                    subNode.SetParent(this);
                    _value.Add(key, subNode);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            SetParent(parent);
        }

        public override object GetObject()
        {
            return _value;
        }

        public void Clear()
        {
            foreach(var kv in _value)
            {
                kv.Value.Close();
            }
            _value.Clear();
        }

        public override void Close()
        {
            foreach(var kv in _value)
            {
                var objKey = kv.Key as FieldBase;
                if(objKey != null)
                {
                    objKey.Close();
                }
                var objValue = kv.Value as MongoObject;
                if(objValue != null)
                {
                    objValue.Close();
                }
            }

            base.Close();
        }

        public override void SetUpdate(bool isupdate = true, IMongoObject subItem = null)
        {
            if (isupdate)
            {
                // 查找修改了的子项
                foreach(var kv in _value)
                {
                    if (ReferenceEquals(kv.Value ,subItem))
                    {
                        if (_ChangedKey.IndexOf(kv.Key) == -1)
                        {
                            _ChangedKey.Add(kv.Key);
                        }
                    }
                }

                _parent?.SetUpdate(isupdate, this);
            }

            IsUpdate = isupdate;
        }

        public IEnumerator<KeyValuePair<Key, TMongoObject>> GetEnumerator()
        {
            return _value.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _value.GetEnumerator();
        }

        public override string ToString()
        {
            return GetBsonValue().ToString();
        }

        public override void Deserialize(IByteBuffer buffer)
        {
            var document = new LazyBsonDocument(buffer);
            IBsonReader reader = new BsonDocumentReader(document);
            reader.ReadStartDocument();
            var bsonType = BsonType.Null;
            while ((bsonType = reader.ReadBsonType()) != BsonType.EndOfDocument)
            {
                var name = reader.ReadName();
                var doc = reader.ReadRawBsonDocument();
                TMongoObject target = new TMongoObject();
                target.Deserialize(doc);
                Key key = (Key)Convert.ChangeType(name, typeof(Key));
                AddValue(key, target);
            }
            reader.ReadEndDocument();
            reader.Close();
        }

        public override string GetFullFieldPath(IMongoObject thisObject = null)
        {
            var key = string.Empty;
            if (thisObject != null)
            {
                foreach(var kv in _value)
                {
                    if (kv.Value == thisObject)
                    {
                        key = kv.Key.ToString();
                        break;
                    }
                }
            }

            if (_parent == null)
            {
                return this.FieldName + "." + key;
            }
            else
            {
                return _parent.GetFullFieldPath(this) + this.FieldName + "." + key;
            }
        }

        public int GetIncreaceKeyInt32()
        {
            var t = typeof(Key);
            if (t == typeof(int))
            {
                int max = 1;
                foreach (var k in Keys)
                {
                    var uk = int.Parse(k.ToString());
                    if (uk > max)
                    {
                        max = uk;
                    }
                }

                return max + 1;
            }

            return 0;
        }

        public long GetIncreaceKeyInt64()
        {
            var t = typeof(Key);
            if (t == typeof(long))
            {
                long max = 1;
                foreach (var k in Keys)
                {
                    var uk = long.Parse(k.ToString());
                    if (uk > max)
                    {
                        max = uk;
                    }
                }

                return max + 1;
            }

            return 0;
        }
    }

}
