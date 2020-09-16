using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace ServerCommon.MongoDB
{
    /// <summary>
    /// 随时间增长的DB对象
    /// 该对象有一个最大值，到达最大值以后停止计时
    /// 每次计时器事件会将对象值增加1，直到到达最大值
    /// 对象值达到最大值后，计时器会停止
    /// 对象值从最大值减少后，计时器又会自动开始
    /// </summary>
    public class TimerRestoreObjectField : FieldBase
    {
        /// <summary>
        /// 值
        /// </summary>
        private int _value;
        /// <summary>
        /// 最后一次更新时间
        /// </summary>
        private DateTime _LastUpdate;
        /// <summary>
        /// 上限值
        /// </summary>
        private int _MaxValue;

        /// <summary>
        /// 时钟间隔
        /// </summary>
        private int _TickInterval;

        public int TickInterval => _TickInterval;

        private System.Timers.Timer _Timer;
        private bool _TimerEnabled = false;

        public TimerRestoreObjectField(string name, IMongoObject parent)
            : base(eFieldType.Object, name, parent)
        {

        }

        public void StartObjectTimer(int initValue, int maxValue, int tickInterval)
        {
            _value = initValue;
            _LastUpdate = DateTime.MinValue;
            _MaxValue = maxValue;
            _TickInterval = tickInterval;
            _Timer = new System.Timers.Timer();
            if (initValue < maxValue)
            {
                StartTimer();
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">mongodb名字</param>
        /// <param name="initValue">初始值</param>
        /// <param name="maxValue">最大值</param>
        /// <param name="tickInterval">时钟间隔(单位毫秒)</param>
        /// <param name="parent"></param>
        public TimerRestoreObjectField(string name, int initValue, int maxValue, int tickInterval, IMongoObject parent) 
            : base(eFieldType.Object, name, parent)
        {
            StartObjectTimer(initValue, maxValue, tickInterval);
            //_value = initValue;
            //_LastUpdate = DateTime.MinValue;
            //_MaxValue = maxValue;
            //_TickInterval = tickInterval;
            //_Timer = new System.Timers.Timer();
            //if(initValue < maxValue)
            //{
            //    StartTimer();
            //}
        }

        public override BsonValue GetBsonValue(bool all)
        {
            BsonDocument docElements = new BsonDocument();
            docElements.Add(_valueName, _value);
            docElements.Add(_maxValueName, _MaxValue);
            docElements.Add(_LastUpdateName, _LastUpdate);
            docElements.Add(_TickIntervalName, _TickInterval);

            return docElements;
        }

        public override BsonValue GetBsonValue(bool all, ref bool hasUpdateValue, ref string key)
        {
            hasUpdateValue = IsUpdate;
            return GetBsonValue(all);
        }

        public override object GetObject()
        {
            return _value;
        }

        private const string _valueName = "val";
        private const string _maxValueName = "maxval";
        private const string _LastUpdateName = "utime";
        private const string _TickIntervalName = "tick";

        public override void ParseBsonValue(BsonValue val, MongoObject parent)
        {
            var doc = val as BsonDocument;
            _value = doc.GetElement(_valueName).Value.AsInt32;
            // _MaxValue = doc.GetElement(_maxValueName).Value.AsInt32;
            _LastUpdate = doc.GetElement(_LastUpdateName).Value.ToLocalTime();
            // _TickInterval = doc.GetElement(_TickIntervalName).Value.AsInt32;

            ResetTickValueAfterLoaded();

            if(_value < _MaxValue)
            {
                StartTimer();
            }
            SetParent(parent);
        }

        public int Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
                SetUpdate(true, this);
                FuncFieldChange?.Invoke(this);
                if(_value < _MaxValue)
                {
                    StartTimer();
                }
                else
                {
                    StopTimer();
                }
            }
        }
        public int MaxValue
        {
            get
            {
                return _MaxValue;
            }
        }
        public DateTime LastUpdate
        {
            get
            {
                return _LastUpdate;
            }
            set
            {
                _LastUpdate = value;
            }
        }

        private void ResetTickValueAfterLoaded()
        {
            if (_TickInterval == 0)
            {
                return;
            }

            var nTick = (int)(DateTime.Now - _LastUpdate).TotalMilliseconds / _TickInterval;
            if(nTick > 0)
            {
                if (_value < _MaxValue)
                {
                    _value = _value + nTick;
                    if (_value > _MaxValue)
                    {
                        _value = _MaxValue;
                        _LastUpdate = DateTime.Now;
                    }
                    else
                    {
                        _LastUpdate = _LastUpdate.AddMilliseconds(nTick * _TickInterval);
                    }
                }
                else
                {
                    _LastUpdate = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// 启动计时器事件
        /// </summary>
        private void StartTimer()
        {
            if(_TimerEnabled)
            {
                return;
            }

            if(_Timer != null)
            {
                _Timer.Close();
                _Timer.Dispose();
            }

            if (_LastUpdate == DateTime.MinValue)
            {
                // _value = _MaxValue;
                _LastUpdate = DateTime.Now;
                SetUpdate(true, this);
                return;
            }

            _Timer = new System.Timers.Timer();

            var ticks = _TickInterval - (int)(DateTime.Now - _LastUpdate).TotalMilliseconds;
            while (ticks <= 0)
            {
                ticks += _TickInterval;
            }
            _Timer.Interval = ticks;

            _Timer.Elapsed += _Timer_Elapsed;
            _Timer.AutoReset = false;

            SetUpdate(true, this);

            _Timer.Start();
            _TimerEnabled = true;
        }

        private void StopTimer()
        {
            // Console.WriteLine("----TimerRestoreObjectField StopTimer----");
            if(_TimerEnabled)
            {
                _TimerEnabled = false;
                _Timer.Close();
                _Timer.Dispose();
                _Timer = null;
            }
        }

        /// <summary>
        /// 时钟事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _TimerEnabled = false;
            if(_value >= _MaxValue)
            {
                return;
            }
            // 计数加1
            _value += 1;
            _LastUpdate = DateTime.Now;
            FuncFieldChange?.Invoke(this);
            // Console.WriteLine("Timer Value + 1 : _value = " + _value);
            SetUpdate(true, this);
            if(_value < _MaxValue)
            {
                StartTimer();
            }
        }

        /// <summary>
        /// 重新设置最大值
        /// </summary>
        /// <param name="val"></param>
        public void SetMaxValue(int val)
        {
            _MaxValue = val;
            SetUpdate(true, this);
            if(_value < _MaxValue)
            {
                StartTimer();
            }
        }

        public override void Close()
        {
            _value = 0;
            _MaxValue = 0;
            _LastUpdate = DateTime.MinValue;
            _TickInterval = int.MaxValue;
            StopTimer();
            _Timer = null;

            base.Close();
        }

        public int GetLeftMinuteSeconds()
        {
            if(Value >= MaxValue)
            {
                return 0;
            }

            return (int)(_TickInterval - (DateTime.Now - LastUpdate).TotalMilliseconds);
        }

    }
}
