using MongoDB.Bson;
using System.Data;
using System;

namespace ServerCommon.MongoDB
{
    public class TField<T> : FieldBase
        where T : IConvertible
    {
        /// <summary>
        /// 限数值类型使用
        /// </summary>
        /// <param name="field"></param>
        /// <param name="oldvalue"></param>
        /// <param name="param"></param>
        public delegate void OnFieldChange2(FieldBase field, T oldvalue, object param = null);
        /// <summary>
        /// 数值改变，只对数值类型起作用，其他类型使用FuncFieldChange
        /// </summary>
        public OnFieldChange2 FuncFieldChange2 { get; set; }

        T _value { get; set; }
        public TField(eFieldType type, string name, IMongoObject parent)
            : base(type, name, parent)
        {
            _value = default(T);
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
        public void SetValue(T val, object param = null, bool isUpdate = true)
        {
            T old = _value;
            _value = val;
            SetUpdate(isUpdate, this);

            switch (FieldType)
            {
                case eFieldType.Double:
                case eFieldType.Int32:
                case eFieldType.Int64:
                    {
                        FuncFieldChange2?.Invoke(this, old, param);
                        FuncFieldChange?.Invoke(this, param);
                    }
                    break;
                default:
                    {
                        FuncFieldChange?.Invoke(this, param);
                    }
                    break;
            }
        }

        public override object GetObject()
        {
            return _value;
        }

        public override BsonValue GetBsonValue(bool all = false)
        {
            switch (FieldType)
            {
                case eFieldType.Double:
                case eFieldType.String:
                case eFieldType.ObjectId:
                case eFieldType.Bool:
                case eFieldType.DateTime:
                case eFieldType.Int32:
                case eFieldType.Int64:
                case eFieldType.Binary:
                    return BsonValue.Create(_value);
                default:
                    throw new System.Exception("GetBsonValue error type:" + FieldType + " name:" + FieldName);
            }
        }

        public override BsonValue GetBsonValue(bool all, ref bool hasUpdateValue, ref string key)
        {
            hasUpdateValue = IsUpdate;
            return GetBsonValue(all);
        }

        public override void ParseBsonValue(BsonValue val, MongoObject parent)
        {
            if (val.IsBsonNull)
            {
                _value = default(T);
            }
            else
            {
                var valConv = ConvertBsonValue(val);
                var typeT = typeof(T);
                if (typeT.IsEnum)
                {
                    _value = (T)Enum.Parse(typeT, valConv.ToString());
                }
                else
                {
                    _value = (T)Convert.ChangeType(valConv, typeT);
                }
                
            }
            SetParent(parent);
        }

        public override void Close()
        {
            FuncFieldChange2 = null;
            _value = default(T);

            base.Close();
        }

        public override string ToString()
        {
            return _value.ToString();
        }
    }
}
