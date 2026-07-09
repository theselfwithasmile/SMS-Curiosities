using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject tokenPrefab;
    
    // Start is called before the first frame update
    void Start()
    {
        //spawn dummy token
        for(int i=0; i<20; i++)
        {
            GameObject token = Instantiate(tokenPrefab, transform.position, Quaternion.identity);
        }
        ContainerManager.Instance.GenerateContainer(capacity: 10, color: Color.red);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
