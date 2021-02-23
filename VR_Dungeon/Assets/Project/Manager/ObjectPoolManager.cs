using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Project.Manager
{
    public class ObjectPoolManager : SingletonBehavior<ObjectPoolManager>
    {
        [Serializable]
        public class PrefabObjectKeyValuePair
        {
            public string name;
            public GameObject prefab;
        }

        public List<PrefabObjectKeyValuePair> prefabs;

        IDictionary<string, IList<GameObject>> _objectPool;

        IDictionary<string, IList<GameObject>> ObjectPool =>
            _objectPool ?? (_objectPool = new Dictionary<string, IList<GameObject>>());

        public GameObject Spawn(string spawnTargetName, Vector3 position = default, Quaternion rot = default)
        {
            var foundedPrefabData = prefabs.FirstOrDefault(obj => obj.name == spawnTargetName);

            if (foundedPrefabData == null) throw new Exception($"{spawnTargetName} don't exist");

            if (!ObjectPool.ContainsKey(spawnTargetName))
                ObjectPool.Add(spawnTargetName, new List<GameObject>());

            var founded = ObjectPool[spawnTargetName].FirstOrDefault(obj => !obj.activeInHierarchy);

            if (founded != null)
                founded.SetActive(true);
            else
            {
                founded = Instantiate(foundedPrefabData.prefab);
                ObjectPool[spawnTargetName].Add(founded);
            }

            if (position != default) founded.transform.position = position;
            if (rot != default) founded.transform.rotation = rot;

            founded.gameObject.SetActive(true);

            return founded;
        }
    } 
}