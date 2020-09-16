using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerCommon.MongoDB
{
    public class TArrayField<T> : FieldBase
        where T : IConvertible
    {
        List<T> _value { get; set; }

        public TArrayField(eFieldType type, string name, IMongoObject parant) 
            : base(type, name, parant)
        {
            _value = new List<T>();
        }

      

        /// <summary>
        /// 获取值
        /// </summary>
        /// <returns></returns>
        public List<T> GetValue()
        {
            return _value;
        }

        public List<T> Value
        {
            get
            {
                return GetValue();
            }
        }

        /// <summary>
        /// 设置值
        /// </summary>
        /// <param name="val"></param>
        /// <param name="param"></param>
        /// <param name="isUpdate"></param>
        public void SetValue(List<T> val, object param = null, bool isUpdate = true)
        {
            _value = val;
            SetUpdate(isUpdate, this);
            FuncFieldChange?.Invoke(this, param);
        }

        public override BsonValue GetBsonValue(bool all, ref bool hasUpdateValue, ref string key)
        {
            BsonArray ba = null;
            hasUpdateValue = false;
            var vtype = typeof(T);
            if (this.IsUpdate)
            {
                // 如果一个字段数据更新了，那么数组里面所有元素都必须写入到doc中，否则会造成数据丢失
                all = true;
            }

            if (all)
            {
                List<BsonValue> tmplist = new List<BsonValue>();
                foreach (var it in _value)
                {
                    tmplist.Add(BsonValue.Create(it));
                    hasUpdateValue = true;
                }
                ba = new BsonArray(tmplist);
            }
            key = this.FieldName;
            return ba;
        }

        public override BsonValue GetBsonValue(bool all = false)
        {
            BsonArray ba = null;
            var vtype = typeof(T);
            if (IsUpdate)
            {
                all = true;
            }

            if (all)
            {
                List<BsonValue> tmplist = new List<BsonValue>();
                foreach (var it in _value)
                {
                    tmplist.Add(BsonValue.Create(it));
                }
                ba = new BsonArray(tmplist);
            }

            return ba;
        }
        public override void ParseBsonValue(BsonValue val, MongoObject parent)
        {
            if(val.IsBsonNull)
            {
                return;
            }
            var array = val as BsonArray;
            foreach (var bv in array)
            {
                T info = (T)Convert.ChangeType(ConvertBsonValue(bv), typeof(T));
                _value.Add(info);
            }
            SetParent(parent);
        }

        public override object GetObject()
        {
            return _value;
        }

        public List<T> GetObject(Func<T, bool> selector)
        {
            var linq = from v in _value where selector(v) select v;
            return linq.ToList();
        }

        public void AddObject(T obj)
        {
            //if (obj is MongoObject)
            //{
            //    var mongoObj = obj as MongoObject;
            //    mongoObj.SetParent(this);
            //}

            _value.Add(obj);
            SetUpdate(true, this);
        }

        public int Count => _value.Count;

        public override void Close()
        {
            _value.Clear();

            base.Close();
        }

        public void Clear()
        {
            _value.Clear();
            SetUpdate(true, this);
        }

        public override string ToString()
        {
            return GetBsonValue().ToString();
        }
    }

}
