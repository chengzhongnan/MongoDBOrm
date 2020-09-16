using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{
    public interface IMongoObject
    {
        void SetUpdate(bool isupdate = true, IMongoObject thisObject = null);
        void SetParent(IMongoObject mapField);

        void UnsetKey(string key, IMongoObject thisObject = null);
        string GetFullFieldPath(IMongoObject thisObject = null);
    }

    public abstract class MongoObject : IMongoObject
    {
        protected Dictionary<string, FieldBase> _fields;
        /// <summary>
        /// 是否有更新
        /// </summary>
        public bool IsUpdate { get; private set; }

        IMongoObject _parent { get; set; }

        public MongoObject()
        {
            _fields = new Dictionary<string, FieldBase>();
            IsColsed = false;
            RegisterFields();
        }

        #region Register
        /// <summary>
        /// 注册字段
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type">类型</param>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public TField<T> RegisterField<T>(eFieldType type, string name)
            where T : IConvertible
        {
            switch (type)
            {
                case eFieldType.Array:
                case eFieldType.Map:
                case eFieldType.Object:
                    throw new Exception("Register field error: " + name);
            } 
            
            if (_fields.ContainsKey(name))
                throw new Exception("Register field repeat: " + name);

            TField<T> field = new TField<T>(type, name, this);
            _fields.Add(name, field);

            return field;
        }
      
        /// <summary>
        /// 注册字段map
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public TMapField<Key, T> RegisterMapField<Key, T>(string name)
            where T: MongoObject, new()
            where Key : IConvertible
        {            
            if (_fields.ContainsKey(name))
                throw new Exception("Register field repeat: " + name);

            TMapField<Key, T> field = new TMapField<Key, T>(eFieldType.Map, name, this);
            _fields.Add(name, field);

            return field;
        }

        /// <summary>
        /// 注册字段map
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">名称</param>
        /// <returns></returns>
        //public TMapField<Key> RegisterMapField<Key>(string name)
        //    where Key : IConvertible
        //{
        //    if (_fields.ContainsKey(name))
        //        throw new Exception("Register field repeat: " + name);

        //    TMapField<Key> field = new TMapField<Key>(eFieldType.Map, name, this);
        //    _fields.Add(name, field);

        //    return field;
        //}

        public void SetParent(IMongoObject parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// 注册字段array
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public TArrayField<T> RegisterArrayField<T>(string name)
            where T : IConvertible
        {
            if (_fields.ContainsKey(name))
                throw new Exception("Register field repeat: " + name);

            TArrayField<T> field = Activator.CreateInstance(typeof(TArrayField<T>), eFieldType.Array, name, this) as TArrayField<T>;
            _fields.Add(name, field);
            return field;
        }

        /// <summary>
        /// 注册字段object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">名称</param>
        /// <returns></returns>
        public TObjectField<T> RegisterObjectField<T>(string name) 
            where T: MongoObject ,new ()
        {            
            if (_fields.ContainsKey(name))
                throw new Exception("Register field repeat: " + name);

            TObjectField<T> field = new TObjectField<T>(eFieldType.Object, name, this);
            _fields.Add(name, field);

            return field;
        }

        /// <summary>
        /// 注册时间更新字段
        /// </summary>
        /// <param name="name">mongodb中的字段</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="maxValue">最大值</param>
        /// <returns></returns>
        public TimerRestoreObjectField RegisterTimerRestoreObjectField(string name, int defaultValue, int maxValue, int tickInterval)
        {
            if (_fields.ContainsKey(name))
                throw new Exception("Register field repeat: " + name);

            TimerRestoreObjectField obj = new TimerRestoreObjectField(name, defaultValue, maxValue, tickInterval, this);
            _fields.Add(name, obj);

            return obj;
        }

        public TimerRestoreObjectField RegisterTimerRestoreObjectField(string name)
        {
            if (_fields.ContainsKey(name))
                throw new Exception("Register field repeat: " + name);

            TimerRestoreObjectField obj = new TimerRestoreObjectField(name, this);
            _fields.Add(name, obj);

            return obj;
        }

        #endregion

        public virtual List<BsonElement> GetBsonValueList(bool all, string fieldName, ref bool hasUpdateValue)
        {
            List<BsonElement> elements = new List<BsonElement>();

            if (all)
            {
                var doc = GetBsonValue(all);
                elements.AddRange(doc.Elements);
                return elements;
            }

            foreach (var it in _fields)
            {
                var field = it.Value;

                switch (field.FieldType)
                {
                    case eFieldType.Binary:
                    case eFieldType.Bool:
                    case eFieldType.DateTime:
                    case eFieldType.Double:
                    case eFieldType.Int32:
                    case eFieldType.Int64:
                    case eFieldType.String:
                    case eFieldType.ObjectId:
                        {
                            if (all || field.IsUpdate)
                            {
                                elements.Add(new BsonElement(fieldName + "." + field.FieldName, field.GetBsonValue(all)));
                                hasUpdateValue = true;
                            }
                        }
                        break;
                    case eFieldType.Object:
                        {
                            var hasSubUpdateValue = false;
                            var subValues = field.GetBsonValueList(all, field.FieldName, ref hasSubUpdateValue);
                            if (hasSubUpdateValue)
                            {
                                elements.AddRange(subValues);
                                hasUpdateValue = true;
                            }
                        }
                        break;
                    case eFieldType.Array:
                    case eFieldType.Map:
                        {
                            var hasSubUpdateValue = false;
                            var key = field.FieldName;
                            var subValue = field.GetBsonValue(all, ref hasSubUpdateValue, ref key);
                            if (hasSubUpdateValue)
                            {
                                if (subValue.IsBsonDocument)
                                {
                                    foreach (var subDoc in subValue.AsBsonDocument)
                                    {
                                        elements.Add(new BsonElement(fieldName + "." + subDoc.Name, subDoc.Value));
                                    }
                                    hasUpdateValue = true;
                                }
                                else if(subValue.IsBsonArray)
                                {
                                    elements.Add(new BsonElement(fieldName + "." + key, subValue));
                                    hasUpdateValue = true;
                                }
                            }
                        }
                        break;
                }

                field.SetUpdate(false, field);
            }

            SetUpdate(false, this);

            return elements;
        }

        public virtual BsonDocument GetBsonValue(bool all, ref bool haveUpdateValue)
        {
            BsonDocument doc = new BsonDocument();

            foreach (var it in _fields)
            {
                var field = it.Value;

                switch (field.FieldType)
                {
                    case eFieldType.Binary:
                    case eFieldType.Bool:
                    case eFieldType.DateTime:
                    case eFieldType.Double:
                    case eFieldType.Int32:
                    case eFieldType.Int64:
                    case eFieldType.String:
                    case eFieldType.ObjectId:
                        {
                            if (all || field.IsUpdate)
                            {
                                doc.Add(field.FieldName, field.GetBsonValue(all));
                                haveUpdateValue = true;
                            }
                        }
                        break;
                    case eFieldType.Object:
                        {
                            var hasSubUpdateValue = false;
                            if (all)
                            {
                                var key = field.FieldName;
                                var subValue = field.GetBsonValue(all, ref hasSubUpdateValue, ref key);
                                doc.Add(field.FieldName, subValue);
                                haveUpdateValue = true;
                            }
                            else
                            {
                                var subValues = field.GetBsonValueList(all, field.FieldName, ref hasSubUpdateValue);
                                if (hasSubUpdateValue)
                                {
                                    doc.AddRange(subValues);
                                    haveUpdateValue = true;
                                }
                            }
                        }
                        break;
                    case eFieldType.Array:
                    case eFieldType.Map:
                        {
                            if (all)
                            {
                                var hasSubUpdateValue = false;
                                var key = field.FieldName;
                                var subValue = field.GetBsonValue(all, ref hasSubUpdateValue, ref key);

                                doc.Add(field.FieldName, subValue);
                                haveUpdateValue = true;
                            }
                            else
                            {
                                var hasSubUpdateValue = false;
                                var key = field.FieldName;
                                var subValue = field.GetBsonValue(all, ref hasSubUpdateValue, ref key) as BsonDocument;
                                if (hasSubUpdateValue)
                                {
                                    doc.AddRange(subValue);
                                    haveUpdateValue = true;
                                }
                            }
                        }
                        break;
                }

                field.SetUpdate(false, field);
            }

            SetUpdate(false, this);

            return doc;
        }

        /// <summary>
        /// 将字段转换为BsonDocument
        /// </summary>
        /// <returns></returns>
        public virtual BsonDocument GetBsonValue(bool all = false)
        {
            try
            {
                var hasUpdateValue = false;
                return GetBsonValue(all, ref hasUpdateValue);
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
        /// <summary>
        /// 将数据转为字段
        /// </summary>
        /// <param name="data"></param>
        public virtual void ParseBsonValue(BsonDocument data)
        {
            foreach (var it in data)
            {
                if (!_fields.ContainsKey(it.Name))
                    continue;

                var field = _fields[it.Name];
                field.ParseBsonValue(it.Value, this);
                field.SetParent(this);
            }
        }

        public void SetUpdate(bool isupdate = true, IMongoObject thisObject = null)
        {
            IsUpdate = isupdate;

            if(isupdate)
                _parent?.SetUpdate(isupdate, this);
        }

        /// <summary>
        /// 注册字段
        /// </summary>
        public abstract void RegisterFields();

        public bool IsColsed { get; private set; }

        public virtual void Close()
        {
            foreach(var kv in _fields)
            {
                kv.Value.Close();
            }
            _fields.Clear();
            IsColsed = true;
        }

        protected List<string> _UnsetKey { get; } = new List<string>();

        public virtual void UnsetKey(string key, IMongoObject self)
        {
            if (_parent == null)
            {
                _UnsetKey.Add(key);
            }
            else
            {
                _parent.UnsetKey(key, this);
            }
        }

        public List<string> GetUnsetKey()
        {
            List<string> result = new List<string>();
            result.AddRange(_UnsetKey);
            _UnsetKey.Clear();
            return result;
        }
        protected Dictionary<string, object> _UndefineObjects { get; } = new Dictionary<string, object>();

        private FieldBase GetField(string name)
        {
            if (_fields.ContainsKey(name))
            {
                return _fields[name];
            }

            return null;
        }

        protected bool SetValueNumber<NumberType>(FieldBase field, NumberType v)
            where NumberType : IConvertible
        {
            var fieldT = field as TField<NumberType>;
            if (fieldT == null)
            {
                return false;
            }

            fieldT.SetValue(v, isUpdate: false);
            return true;
        }

        protected bool SetFieldValue<TFieldType>(string name, TFieldType v)
            where TFieldType : IConvertible
        {
            var field = GetField(name);
            if (field == null)
            {
                _UndefineObjects.Add(name, v);
                return false;
            }

            if (field.FieldType == eFieldType.Int32 || field.FieldType == eFieldType.Int64)
            {
                var bRet = SetValueNumber<int>(field, v.ToInt32(CultureInfo.CurrentCulture));
                bRet = bRet || SetValueNumber<uint>(field, v.ToUInt32(CultureInfo.CurrentCulture));
                bRet = bRet || SetValueNumber<long>(field, v.ToInt64(CultureInfo.CurrentCulture));
                bRet = bRet || SetValueNumber<ulong>(field, v.ToUInt64(CultureInfo.CurrentCulture));
                bRet = bRet || SetValueNumber<short>(field, v.ToInt16(CultureInfo.CurrentCulture));
                bRet = bRet || SetValueNumber<ushort>(field, v.ToUInt16(CultureInfo.CurrentCulture));

                if (!bRet)
                {
                    throw new ContextMarshalException(name);
                }

                return true;
            }
            else
            {
                var fieldT = field as TField<TFieldType>;
                if (fieldT == null)
                {
                    throw new ContextMarshalException(name);
                }

                fieldT.SetValue(v, isUpdate: false);
                return true;
            }
        }
        protected void ParseBsonBody(IBsonReader bsonReader, BsonType bsonType)
        {
            var name = bsonReader.ReadName();
            switch (bsonType)
            {
                case BsonType.Array:
                    {
                        var array = bsonReader.ReadRawBsonArray();
                        var field = GetField(name);
                        if (field != null)
                            field.Deserialize(array);
                    }
                    break;
                case BsonType.Binary:
                    {
                        var bin = bsonReader.ReadBinaryData();
                        var field = GetField(name);
                        if (field != null)
                            field.Deserialize(bin);
                    }
                    break;
                case BsonType.Boolean:
                    {
                        var bValue = bsonReader.ReadBoolean();
                        SetFieldValue(name, bValue);
                    }
                    break;
                case BsonType.DateTime:
                    {
                        var dt = new DateTime(bsonReader.ReadDateTime());
                        SetFieldValue(name, dt);
                    }
                    break;
                case BsonType.Decimal128:
                    {
                        var dec = bsonReader.ReadDecimal128();
                        SetFieldValue(name, dec);
                    }
                    break;
                case BsonType.Document:
                    {
                        var doc = bsonReader.ReadRawBsonDocument();
                        var field = GetField(name);
                        if (field != null)
                            field.Deserialize(doc);
                    }
                    break;
                case BsonType.Double:
                    {
                        var dValue = bsonReader.ReadDouble();
                        SetFieldValue(name, dValue);
                    }
                    break;
                case BsonType.Int32:
                    {
                        var iValue = bsonReader.ReadInt32();
                        SetFieldValue(name, iValue);
                    }
                    break;
                case BsonType.Int64:
                    {
                        var lValue = bsonReader.ReadInt64();
                        SetFieldValue(name, lValue);
                    }
                    break;
                case BsonType.JavaScript:
                    {
                        var js = bsonReader.ReadJavaScript();
                        SetFieldValue(name, js);
                    }
                    break;
                case BsonType.JavaScriptWithScope:
                    {
                        var js = bsonReader.ReadJavaScriptWithScope();
                        SetFieldValue(name, js);
                    }
                    break;
                case BsonType.ObjectId:
                    {
                        var objectid = bsonReader.ReadObjectId();
                        SetFieldValue(name, objectid);
                    }
                    break;
                case BsonType.RegularExpression:
                    {
                        var reg = bsonReader.ReadRegularExpression();
                        var field = GetField(name);
                        if (field != null)
                            field.Deserialize(reg);
                    }
                    break;
                case BsonType.String:
                    {
                        var sValue = bsonReader.ReadString();
                        SetFieldValue(name, sValue);
                    }
                    break;
                case BsonType.Symbol:
                    {
                        var symbol = bsonReader.ReadSymbol();
                        SetFieldValue(name, symbol);
                    }
                    break;
                case BsonType.Timestamp:
                    {
                        var time = bsonReader.ReadTimestamp();
                        SetFieldValue(name, time);
                    }
                    break;
                default:
                    break;
            }
        }

        public virtual void Deserialize(IByteBuffer buffer)
        {
            var document = new LazyBsonDocument(buffer);
            IBsonReader reader = new BsonDocumentReader(document);
            reader.ReadStartDocument();
            var bsonType = BsonType.Null;
            while ((bsonType = reader.ReadBsonType()) != BsonType.EndOfDocument)
            {
                ParseBsonBody(reader, bsonType);
            }
            reader.ReadEndDocument();
            reader.Close();
        }

        public virtual void Deserialize(BsonBinaryData binaryData)
        {

        }
        public virtual void Deserialize(BsonRegularExpression reg)
        {

        }

        public virtual string GetFullFieldPath(IMongoObject thisObject = null)
        {
            if (_parent == null)
            {
                return string.Empty;
            }

            return _parent.GetFullFieldPath(this);
        }
    }

    public abstract class MongoObject<T> : MongoObject, IBsonSerializer<T>
     where T : MongoObject, new()
    {
        public Type ValueType => typeof(T);

        public T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var it = (new T()) as IBsonSerializer;
            return it.Deserialize(context, args) as T;
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T value)
        {
            throw new NotImplementedException();
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            throw new NotImplementedException();
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonReader = context.Reader;
            var bsonType = BsonType.Null;
            bsonReader.ReadStartDocument();
            while ((bsonType = bsonReader.ReadBsonType()) != BsonType.EndOfDocument)
            {
                ParseBsonBody(bsonReader, bsonType);
            }
            bsonReader.ReadEndDocument();
            return this;
        }

    }
}
