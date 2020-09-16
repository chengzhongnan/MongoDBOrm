using MongoDB.Bson;
using MongoDB.Bson.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{
    public class TObjectField<T> : FieldBase
        where T : MongoObject, new ()
    {
        T _value { get; set; }

        public TObjectField(eFieldType type, string name, IMongoObject parent) 
            : base(type, name, parent)
        {
            _value = new T();
        }


        /// <summary>
        /// 获取值
        /// </summary>
        /// <returns></returns>
        public T GetValue()
        {
            return _value;
        }

        public T Value
        {
            get
            {
                return _value;
            }
        }

        /// <summary>
        /// 设置值
        /// </summary>
        /// <param name="val"></param>
        /// <param name="param"></param>
        /// <param name="isUpdate"></param>
        public virtual void SetValue(T val, object param = null, bool isUpdate = true)
        {
            _value = val;
            SetUpdate(isUpdate, this);
            FuncFieldChange?.Invoke(this, param);
        }

        
        public override BsonValue GetBsonValue(bool all = false)
        {
            return _value.GetBsonValue(all);
        }

        public override BsonValue GetBsonValue(bool all, ref bool hasUpdateValue, ref string key)
        {
            key = key + "." + FieldName;
            return _value.GetBsonValue(all, ref hasUpdateValue);
        }

        public override List<BsonElement> GetBsonValueList(bool all, string fieldName, ref bool hasUpdateValue)
        {
            return _value.GetBsonValueList(all, fieldName, ref hasUpdateValue);
        }

        public override void ParseBsonValue(BsonValue val, MongoObject parent)
        {
            var doc = val as BsonDocument;
            _value.ParseBsonValue(doc);
            _value.SetParent(this);
            SetParent(parent);
        }

        public override object GetObject()
        {
            return _value;
        }

        public override void Close()
        {
            _value.Close();
            base.Close();
        }

        public override string ToString()
        {
            return _value.GetBsonValue().ToString();
        }

        public override void Deserialize(IByteBuffer buffer)
        {
            throw new NotImplementedException();
        }
    }
}
