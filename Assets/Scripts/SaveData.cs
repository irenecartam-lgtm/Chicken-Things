using UnityEngine;


[System.Serializable]
public class SaveData
{
    public int score;
    public int life;
    public Vector3 playerPosition;
    public int wave;   // oleada que se retoma al cargar (0 = partida antigua, sin oleada)
}