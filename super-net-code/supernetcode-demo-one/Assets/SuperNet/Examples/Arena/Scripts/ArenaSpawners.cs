using SuperNet.Unity.Components;
using UnityEngine;

namespace SuperNet.Examples.Arena {

	public class ArenaSpawners : MonoBehaviour {

		// Scene objects
		public Menu Menu;
		public BoxCollider[] Spawns;
		public NetworkSpawner SpawnerPlayers;
		public NetworkSpawner SpawnerSpheres;
		public NetworkSpawner SpawnerCubes;
		public ArenaClient ArenaClient;
		public ArenaServer ArenaServer;
		public ArenaRelay ArenaRelay;

		private void Reset() {
			Menu = transform.Find("/Canvas").GetComponent<Menu>();
			Spawns = transform.Find("/Spawners/Spawns").GetComponentsInChildren<BoxCollider>();
			SpawnerPlayers = transform.Find("/Spawners/Player").GetComponent<NetworkSpawner>();
			SpawnerSpheres = transform.Find("/Spawners/Spheres").GetComponent<NetworkSpawner>();
			SpawnerCubes = transform.Find("/Spawners/Cubes").GetComponent<NetworkSpawner>();
			ArenaClient = FindObjectOfType<ArenaClient>();
			ArenaServer = FindObjectOfType<ArenaServer>();
			ArenaRelay = FindObjectOfType<ArenaRelay>();
		}

		public Vector3 GetRandomSpawnPosition(float height) {
			BoxCollider area = Spawns[Random.Range(0, Spawns.Length)];
			float x = Random.Range(area.bounds.min.x, area.bounds.max.x);
			float y = area.bounds.min.y + height;
			float z = Random.Range(area.bounds.min.z, area.bounds.max.z);
			return new Vector3(x, y, z);
		}

		public Quaternion GetRandomPlayerRotation() {
			return Quaternion.Euler(
				0f,
				Random.Range(0f, 360f),
				0f
			);
		}

		public Quaternion GetRandomMovableRotation() {
			return Quaternion.Euler(
				Random.Range(0f, 360f),
				Random.Range(0f, 360f),
				Random.Range(0f, 360f)
			);
		}

		public PlayerController SpawnPlayer() {
			Vector3 position = GetRandomSpawnPosition(2f);
			Quaternion rotation = GetRandomPlayerRotation();
			NetworkPrefab instance = SpawnerPlayers.Spawn(position, rotation);
			return instance.GetComponent<PlayerController>();
		}

		public Movable SpawnCube() {
			Vector3 position = GetRandomSpawnPosition(10f);
			Quaternion rotation = GetRandomMovableRotation();
			NetworkPrefab instance = SpawnerCubes.Spawn(position, rotation);
			return instance.GetComponent<Movable>();
		}

		public Movable SpawnSphere() {
			Vector3 position = GetRandomSpawnPosition(5f);
			Quaternion rotation = GetRandomMovableRotation();
			NetworkPrefab instance = SpawnerSpheres.Spawn(position, rotation);
			return instance.GetComponent<Movable>();
		}

		public void DespawnAllCubes() {
			NetworkPrefab[] list = SpawnerCubes.GetSpawnedPrefabs();
			foreach (NetworkPrefab obj in list) {
				Destroy(obj.gameObject);
			}
		}

		public void DespawnAllSpheres() {
			NetworkPrefab[] list = SpawnerSpheres.GetSpawnedPrefabs();
			foreach (NetworkPrefab obj in list) {
				Destroy(obj.gameObject);
			}
		}

		public void DespawnAllPlayers() {
			NetworkPrefab[] list = SpawnerPlayers.GetSpawnedPrefabs();
			foreach (NetworkPrefab obj in list) {
				Destroy(obj.gameObject);
			}
		}

		public void ClaimAuthorityOnAllCubes() {
			NetworkPrefab[] list = SpawnerCubes.GetSpawnedPrefabs();
			foreach (NetworkPrefab obj in list) {
				obj.GetComponent<Movable>().ClaimAuthority();
			}
		}

		public void ClaimAuthorityOnAllSpheres() {
			NetworkPrefab[] list = SpawnerSpheres.GetSpawnedPrefabs();
			foreach (NetworkPrefab obj in list) {
				obj.GetComponent<Movable>().ClaimAuthority();
			}
		}

		public void SetRemoteSpawning(bool enabled) {

			// This method either allows or dissalows remote spawning
			// This is done mainly for security on the server
			// If its disabled on the server, then clients can't spawn/despawn their own objects
			// On clients this must be anbled to make sure the server can spawn objects

			SpawnerPlayers.IgnoreRemoteDespawns = !enabled;
			SpawnerPlayers.IgnoreRemoteSpawns = !enabled;
			SpawnerSpheres.IgnoreRemoteDespawns = !enabled;
			SpawnerSpheres.IgnoreRemoteSpawns = !enabled;
			SpawnerCubes.IgnoreRemoteDespawns = !enabled;
			SpawnerCubes.IgnoreRemoteSpawns = !enabled;

		}

	}

}
