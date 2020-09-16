using MongoDB.Bson;
using MongoDB.Bson.IO;
using System.Collections.Generic;

namespace ServerCommon.MongoDB
{
    public abstract class FieldBase : IMongoObject
    {
        public delegate void OnFieldChange(FieldBase field, object param=null);

        protected IMongoObject _parent { get; set; }

        /// <summary>
        /// 字段类型
        /// </summary>
        public eFieldType FieldType { get; private set; }
        /// <summary>
        /// 值改变
        /// </summary>
        public OnFieldChange FuncFieldChange { get; set; }       
        /// <summary>
        /// 字段是否更新
        /// </summary>
        public bool IsUpdate { get; protected set; }
        /// 字段名
        /// </summary>
        public string FieldName { get; private set; }


        public FieldBase(eFieldType type, string name, IMongoObject parent)
        {
            FieldType = type;
            IsUpdate = false;
            FieldName = name;
            _parent = parent;
        }

        public virtual void SetUpdate(bool isupdate = true, IMongoObject subItem = null)
        {
            IsUpdate = isupdate;

            if(IsUpdate)
                _parent?.SetUpdate(isupdate, this);
        }
        /// <summary>
        /// 获取值(Bson)
        /// </summary>
        /// <returns></returns>
        public abstract BsonValue GetBsonValue(bool all);
        public abstract BsonValue GetBsonValue(bool all, ref bool hasUpdateValue, ref string key);
        /// <summary>
        /// 解析字段(bson)
        /// </summary>
        /// <param name="val"></param>
        public abstract void ParseBsonValue(BsonValue val, MongoObject parent);

        public virtual List<BsonElement> GetBsonValueList(bool all, string fieldName, ref bool hasUpdateValue)
        {
            return new List<BsonElement>();
        }

        /// <summary>
        /// 获取值(object)
        /// </summary>
        /// <returns></returns>
        public abstract object GetObject();

        /// <summary>
        /// 转换BosnValue(基础类型)
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public object ConvertBsonValue(BsonValue val)
        {
            switch(val.BsonType)
            {
                case BsonType.Double:
                    return val.AsDouble;
                    
                case BsonType.String:
                    return val.AsString;
                    
                case BsonType.ObjectId:
                    return val.AsObjectId;
                    
                case BsonType.Boolean:
                    return val.AsBoolean;
                    
                case BsonType.DateTime:
                    return val.ToLocalTime();
                    
                case BsonType.Int32:
                    return val.AsInt32;
                    
                case BsonType.Int64:
                    return val.AsInt64;
                    
                case BsonType.Binary:
                    return val.AsByteArray;
                    
                default:
                    throw new System.Exception("PaseBsonValue error bsontype:" + val.BsonType + " name:"+ FieldName + " type:" + FieldType);

            }
        }
        
        public virtual void Close()
        {
            FuncFieldChange = null;
            IsUpdate = false;
            FieldName = string.Empty;
            _parent = null;
        }

        public void SetParent(IMongoObject parent)
        {
            _parent = parent;
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
                _parent.UnsetKey(this.FieldName + "." + key);
            }
        }

        public virtual void Deserialize(IByteBuffer buffer)
        {

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
                return this.FieldName;
            }

            return _parent.GetFullFieldPath(this) + "." + FieldName;
        }
    }
}
