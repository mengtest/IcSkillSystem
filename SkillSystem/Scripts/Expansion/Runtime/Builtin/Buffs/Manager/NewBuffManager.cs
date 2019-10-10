﻿using System;
using System.Collections.Generic;
using System.Linq;
using CabinIcarus.IcSkillSystem.Runtime.Buffs;
using CabinIcarus.IcSkillSystem.Runtime.Buffs.Components;
using CabinIcarus.IcSkillSystem.Runtime.Buffs.Entitys;
using CabinIcarus.IcSkillSystem.Runtime.Buffs.Systems;
using CabinIcarus.IcSkillSystem.Runtime.Buffs.Systems.Interfaces;
using CabinIcarus.IcSkillSystem.SkillSystem.Runtime.Utils;
using Debug = UnityEngine.Debug;

namespace CabinIcarus.IcSkillSystem.Expansion.Runtime.Builtin.Buffs
{
    interface IBuffList
    {
        int Count { get; }
    }

    class BuffList<T>:FasterList<T>,IBuffList
    {
        public FasterReadOnlyList<T> Buffs => AsReadOnly();
    }

    class CreateAndDestroyEventInfo
    {
        public Action<BuffEntity, int> OnCreate;
        public Action<BuffEntity, int> OnDestroy;
    }
    
    public class NewBuffManager:INewBuffManager
    {
        public FasterReadOnlyList<BuffEntity> Entitys => _entitys.AsReadOnly();
        private IBuffList _currentBuffs;
        private FasterList<AIcStructBuffSystem> _systemMap;
        private FasterList<IBuffUpdateSystem> _updateSystems;
        private FasterList<BuffEntity> _entitys;
        private Dictionary<BuffEntity, Dictionary<Type,IBuffList>> _buffMaps;

        public NewBuffManager()
        {
            _entitys = new FasterList<BuffEntity>();
            _buffMaps = new Dictionary<BuffEntity, Dictionary<Type,IBuffList>>();
            _systemMap = new FasterList<AIcStructBuffSystem>();
            _updateSystems = new FasterList<IBuffUpdateSystem>();
        }

        public INewBuffManager AddBuffSystem<T>(T structBuffSystem) where T : AIcStructBuffSystem
        {
#if UNITY_EDITOR
//            if (_systemMap.Contains(structBuffSystem))
//            {
//                Debug.LogWarning($"{structBuffSystem.GetType()} System already exists, skip");
//                return this;
//            }
#endif
            _systemMap.Add(structBuffSystem);
            _updateSystems.Add(structBuffSystem);
            return this;
        }

        private int _id = -1;

        /// <summary>
        /// id累加创建实体
        /// </summary>
        /// <returns></returns>
        public BuffEntity CreateEntity()
        {
            ++_id;
            
            return CreateEntity(_id);
        }
        
        /// <summary>
        /// 创建实体
        /// </summary>
        /// <param name="id">id存在的话将创建失败</param>
        /// <returns>id为-1为创建失败</returns>
        public BuffEntity CreateEntity(int id)
        {
            var entity = id;

            if (_entitys.Contains(entity))
            {
                return -1;
            }
            
            _entitys.Add(entity);
            _buffMaps.Add(entity,new Dictionary<Type, IBuffList>());
            return entity;
        }
        
        /// <summary>
        /// 删除实体
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public bool DestroyEntity(BuffEntity entity)
        {
            if (!_checkEntityExist(entity))
            {
                return false;
            }

            //id减一下;
            if (entity == _id)
            {
                --_id;
            }
            
            _entitys.Remove(entity);

            _buffMaps.Remove(entity);
            
            return true;
        }

        public void AddBuff<T>(BuffEntity entity,in T buff) where T : struct, IBuffDataComponent
        {
            _checkType<T>();
            if (!_checkEntityExist(entity))
            {
                return;
            }
            _addBuff(entity, buff);
        }

        private void _addBuff<T>(BuffEntity entity, T buff) where T : struct, IBuffDataComponent
        {
            BuffList<T> buffList;
            var type = typeof(T);

            var buffMap = _buffMaps[entity];

            if (!buffMap.TryGetValue(type, out var result))
            {
                buffList = new BuffList<T>();
                buffMap.Add(type, buffList);
            }
            else
            {
                buffList = (BuffList<T>) result;
            }

            buffList.Add(buff);
            _currentBuffs = buffList;
            _callSystem(entity,buffList.Count - 1, true);
        }

        /// <summary>
        /// Systme中查询
        /// </summary>
        /// <param name="index"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetCurrentBuffData<T>(int index) where T : struct, IBuffDataComponent
        {
            if (_currentBuffs.Count - 1 < index)
            {
                throw new IndexOutOfRangeException($"{typeof(T).Name} Buff Count :{_currentBuffs.Count},get index :{index}");
            }
            return ((BuffList<T>) _currentBuffs)[index];
        }
        
        public T GetBuffData<T>(BuffEntity entity, int index) where T : struct, IBuffDataComponent
        {
            if (!_checkEntityExist(entity))
            {
                throw new ArgumentException($"{entity.ID} entity not exist! Please Call {nameof(CreateEntity)}.");
            }
            
            var type = typeof(T);
            var buffMap = _buffMaps[entity];

            if (!buffMap.TryGetValue(type, out var result))
            {
                throw new ArgumentException($"no {type.Name} Buff! Please Call {nameof(AddBuff)} or {nameof(SetBuffData)}.");
            }

            if (result.Count - 1 < index)
            {
                throw new IndexOutOfRangeException($"{type.Name} Buff Count :{result.Count},get index :{index}");
            }

            return ((BuffList<T>)result)[index];
        }

        /// <summary>
        /// 设置buff值,如果不存在指定类型的buff或索引超出就会进行添加
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="buff"></param>
        /// <param name="index"></param>
        /// <typeparam name="T"></typeparam>
        public void SetBuffData<T>(BuffEntity entity, in T buff,int index) where T : struct, IBuffDataComponent
        {
            _checkType<T>();
            if (!_checkEntityExist(entity))
            {
                return;
            }
            var type = typeof(T);
            var buffMap = _buffMaps[entity];
            
            if (!buffMap.TryGetValue(type,out var result))
            {
                _addBuff(entity,buff);
                return;
            }
            
            BuffList<T> buffList = (BuffList<T>) result;

            if (index >= buffList.Count -1)
            {
                _addBuff(entity,buff);
                return;
            }

            buffList[index] = buff;
        }

        private void _callSystem(BuffEntity entity,int index,bool isCreate)
        {
            foreach (AIcStructBuffSystem system in _systemMap)
            {
                if (isCreate)
                {
                    system.Create(entity,index);
                }
                else
                {
                    system.Destroy(entity,index);
                }
            }
        }

        private bool _checkEntityExist(BuffEntity entity)
        {
            return _entitys.Contains(entity);
        }

        private static void _checkType<T>() where T : struct, IBuffDataComponent
        {
#if UNITY_EDITOR && IcSkillSystemDebug
            var type = typeof(T);

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var fieldInfo in fields)
            {
                IcCheck.True(fieldInfo.FieldType.IsValueType,$"{type.Name}.{fieldInfo.Name} not is Value Type.");
            }

            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var propertyInfo in properties)
            {
                IcCheck.True(propertyInfo.PropertyType.IsValueType,$"{type.Name}.{propertyInfo.Name} not is Value Type.");
            }
#endif
        }

        public bool RemoveBuff<T>(BuffEntity entity, T buff) where T : struct, IBuffDataComponent
        {
            if (!_checkEntityExist(entity))
            {
                return false;
            }

            var buffMap = _buffMaps[entity];

            var type = typeof(T);
            if (buffMap.TryGetValue(type,out var result))
            {
                var buffList = (BuffList<T>) result;
                for (var index = buffList.Count - 1; index >= 0; index--)
                {
                    var bf = buffList[index];
                    if (bf.Equals(buff))
                    {
                        _currentBuffs = buffList;
                        buffList.RemoveAt(index);
                        _callSystem(entity, index, false);
                        return true;
                    }
                }
            }
            
            return false;
        }

        public bool HasBuff<T>(BuffEntity entity,T buff) where T : struct, IBuffDataComponent
        {
            if (!_checkEntityExist(entity))
            {
                return false;
            }
            
            var buffMap = _buffMaps[entity];
            
            if (buffMap.TryGetValue(typeof(T),out var result))
            {
                var buffList = (BuffList<T>) result;
                foreach (var bf in buffList)
                {
                    if (buff.Equals(bf))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public IEnumerable<T> GetBuffs<T>(BuffEntity entity,T condition) where T : IBuffDataComponent
        {
            var buffs = GetBuffs<T>(entity);

            if (buffs.Count > 0)
            {
                return buffs.Where(x => x.Equals(condition));
            }
            
            return FasterReadOnlyList<T>.DefaultList;
        }
        
        public FasterReadOnlyList<T> GetBuffs<T>(BuffEntity entity) where T : IBuffDataComponent
        {
            if (_checkEntityExist(entity))
            {
                var buffMap = _buffMaps[entity];
            
                if (buffMap.TryGetValue(typeof(T), out var result))
                {
                    return ((BuffList<T>)result).Buffs;
                }
            }
            
            return FasterReadOnlyList<T>.DefaultList;
        }
        
        public int GetBuffCount<T>(BuffEntity entity) where T : IBuffDataComponent
        {
            int count = 0;
            
            if (!_checkEntityExist(entity))
            {
                return count;
            }
            
            var buffMap = _buffMaps[entity];
            
            if (buffMap.TryGetValue(typeof(T), out var result))
            {
                count += result.Count;
            }

            return count;
        }
        
        public void Update()
        {
            foreach (var updateSystem in _updateSystems)
            {
                updateSystem.Execute();
            }
        }
    }
}