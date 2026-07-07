using UnityEngine;
using TMPro;
using System.Collections;

public class Countdown : MonoBehaviour
{
    public TextMeshProUGUI countdownText;

    IEnumerator Start()
    {
        countdownText.text = "3";
        yield return new WaitForSeconds(1);

        countdownText.text = "2";
        yield return new WaitForSeconds(1);

        countdownText.text = "1";
        yield return new WaitForSeconds(1);

        countdownText.text = "GO!";
        yield return new WaitForSeconds(1);

        countdownText.gameObject.SetActive(false);
    }
}