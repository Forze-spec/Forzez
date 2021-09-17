using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader {
    private Vector2? _spawnPosition;
    [CanBeNull] private string _checkpointId;
    [CanBeNull] private string _transitionId;

    private GameObject _playerObject;

    public Scene CurrentScene => SceneManager.GetActiveScene();

    private void ClearSpawnData() {
        _spawnPosition = null;
        _checkpointId = null;
        _transitionId = null;
    }
    
    private void SceneLoadedHandler(Scene scene, LoadSceneMode mode) {
        if (!_playerObject) {
            return;
        }

        if (_spawnPosition != null) {
            _playerObject.transform.position = (Vector3)_spawnPosition;
        } else if (_checkpointId != null) {
            var checkpoint = GameObject
                .FindGameObjectsWithTag(Tags.Checkpoint)
                .Select(elem => elem.GetComponent<Checkpoint>())
                .FirstOrDefault(elem => elem.checkpointId == _checkpointId);
            if (checkpoint == null) { // TODO check
                throw new Exception($"Init position error. Checkpoint {_checkpointId} not found");
            }
            
            // TODO брать точку спавна а не checkpoint.transform.position
            _playerObject.transform.position = (Vector2)checkpoint.transform.position;
        } else if (_transitionId != null) {
            var sceneTransition = GameObject
                .FindGameObjectsWithTag(Tags.SceneTransition)
                .Select(elem => elem.GetComponent<SceneTransition>())
                .FirstOrDefault(elem => elem.transitionId == _transitionId);
            if (sceneTransition == null) { // TODO check
                throw new Exception($"Init position error. Scene transition {_transitionId} not found");
            }
            
            _playerObject.transform.position = (Vector2)sceneTransition.spawnPoint.position;
        }
    }

    public void Init(GameObject playerObject) {
        _playerObject = playerObject;
        SceneManager.sceneLoaded += SceneLoadedHandler;
    }

    public void Clear() {
        _playerObject = null;
        SceneManager.sceneLoaded -= SceneLoadedHandler;
    }

    public void LoadScene(string sceneName) {
        ClearSpawnData();
        SceneManager.LoadScene(sceneName);
    }

    public void LoadSceneWithSpawnPosition(string sceneName, Vector2 playerStartPosition) {
        ClearSpawnData();
        _spawnPosition = playerStartPosition;
        SceneManager.LoadScene(sceneName);
    }
    
    public void LoadSceneWithCheckpoint(string sceneName, string checkpointId) {
        ClearSpawnData();
        _checkpointId = checkpointId;
        SceneManager.LoadScene(sceneName);
    }

    public void LoadSceneWithTransition(string sceneName, string transitionId) {
        ClearSpawnData();
        _transitionId = transitionId;
        SceneManager.LoadScene(sceneName);
    }
}