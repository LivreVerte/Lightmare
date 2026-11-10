using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KModkit;
using lvLightmare;

using Rnd = UnityEngine.Random;

public class LightmareScript : MonoBehaviour {
	public KMAudio Audio;
	public KMBombInfo bomb;
	public KMBombModule module;
	public KMColorblindMode ColorblindMode;

	public Mesh[] shapes = new Mesh[11];
	public Sprite[] patterns = new Sprite[7];
	public GameObject[] leds = new GameObject[7];
	public KMSelectable Button;
	public GameObject Highlight; 
	public TextMesh Display;
	public TextMesh[] colorBlind = new TextMesh[7];
	public Material[] buttonColors = new Material[8];
	public Material[] ledColors = new Material[9];
	public KMSelectable selectable;
	public AudioClip[] sounds = new AudioClip[1];

	const String BASE36ALPHABET = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
	private readonly String[] DISPLAYS = {"Submit", "Done", "Black", "Red", "Yellow", "Detonate", "Hold", "Tap", "Blue", "Green", "Magenta", "Cyan", "True", "False", "Solve", "Strike", "One", "Two", "Three", "Four", "Five", "Six", "Seven"};
	private int buttonColor;
	private String Label;

	sealed class WireInfo
    {
        public int TopConnector;
        public int BottomConnector;
        public int Level;
        public MeshFilter MeshFilter;
        public MeshFilter HighlightMeshFilter;
		public bool cross;
    }
	private readonly WireInfo[] wires = new WireInfo[7];
	static int moduleIdCounter = 1;
	static int lmCounter = 1;
    int moduleID;
	int lmID;
    private bool moduleSolved = false;
	String Serial;
	int[] order = new int[6];
	int[] decoded = new int[6];
	int shape;
	private int pattern;
	private int labelIndex;
	int[,] ledFlashes = {{8, -1, -1, -1, -1}, {8, -1, -1, -1, -1}, {8, -1, -1, -1, -1}, {8, -1, -1, -1, -1}, {8, -1, -1, -1, -1}, {8, -1, -1, -1, -1}, {8, -1, -1, -1, -1}};
	int excluded;
	int[] ledStatus = new int[7];
	bool initialized = false;
	bool anyCross = false;
	int[] base36 = {0, 0, 0, 0, 0, 0};
	int currentColor = 0;
	int startingColor;
	int finalColor;
	int stage = 0;
	int[] chosenIndex = new int[4];
	public GameObject[] stageLights = new GameObject[4];
	int[] pressedColors = new int[4];
	private bool colorblindModeEnabled;

	void Awake(){
		
		selectable.OnInteractEnded += ButtonRelease;
		selectable.OnInteract += delegate () { ButtonPress(selectable); return false;};
		moduleID = moduleIdCounter++;
		lmID = lmCounter++;
		Debug.LogFormat("[Lightmare #{0}] Commencing: Version 1:1:0",moduleID);
		StartCoroutine(ChooseMeshes());
		
		if(initialized == false){
			StartCoroutine(Initialize());
		} else {
			
		}
	}
	void ButtonRelease(){
		selectable.transform.localPosition = new Vector3(0, 1f, 0);
		return;
	}
	void Start(){
		
	}
	void onStrike(){
		stage = 0;
		foreach(GameObject s in stageLights){s.GetComponent<MeshRenderer>().material = ledColors[8];}
		currentColor = startingColor;
	}
	void ButtonPress(KMSelectable selected){
		if(moduleSolved == true){
			return;
		}
		selected.AddInteractionPunch();
		selected.transform.localPosition = new Vector3(0, 0.8f, 0);
		int orderIndex = order[stage]-1;
		Debug.LogFormat("[Lightmare #{0}] User pressed the button. At this time, LED {1} is {2}. ", moduleID, orderIndex+1, NumToCol(ledFlashes[orderIndex, ledStatus[orderIndex]]));
		if(ledStatus[orderIndex] == 0){
			Debug.LogFormat("[Lightmare #{0}] Tried pressing when the LED was off. Nothing happened. ",moduleID);
			return;
		}
		int selectedColor = ledFlashes[orderIndex, ledStatus[orderIndex]];
		if(selectedColor == finalColor || selectedColor == ledFlashes[excluded, stage + 1]){
			Debug.LogFormat("[Lightmare #{0}] Tried pressing when the LED displayed a restricted color. Strike!",moduleID);
			GetComponent<KMAudio>().PlaySoundAtTransform(sounds[2].name, transform);
			GetComponent<KMBombModule>().HandleStrike();
			onStrike();
			return;
		}
		if(stage != 3){
			currentColor = selectedColor ^ currentColor;
			//Debug.Log("You pressed " + NumToCol(selectedColor));
			//Debug.Log("Current Color is " + NumToCol(currentColor));
			stageLights[stage].GetComponent<MeshRenderer>().material = ledColors[2];
			pressedColors[stage] = selectedColor;
			stage++;
			Debug.LogFormat("[Lightmare #{0}] Successfully pressed the button. Color is now {1}. User has made {2} inputs. ",moduleID, NumToCol(currentColor), stage);
			GetComponent<KMAudio>().PlaySoundAtTransform(sounds[1].name, transform);
			return;
		}
		currentColor = selectedColor ^ currentColor;
		Debug.LogFormat("[Lightmare #{0}] Final press initiated. Lets see how the user does. ",moduleID);
		Debug.LogFormat("[Lightmare #{0}] The color the user attempted to submit was {1}.",moduleID, NumToCol(currentColor));
		pressedColors[3] = selectedColor;
		if(currentColor != finalColor){
			Debug.LogFormat("[Lightmare #{0}] User got the incorrect final color. Strike! ",moduleID);
			GetComponent<KMBombModule>().HandleStrike();
			GetComponent<KMAudio>().PlaySoundAtTransform(sounds[2].name, transform);
			onStrike();
			return;
		}
		if(currentColor == finalColor){
			Debug.LogFormat("[Lightmare #{0}] You did it! Time to play the saxophone. ",moduleID);
			StartCoroutine(OnSolve());
		}
	}
	private IEnumerator Epologue(){
		yield return null;
		int i = 0;
		int j = 0;
		while(true){
			(leds[(i)%7]).GetComponent<MeshRenderer>().material = ledColors[8];
			(leds[(i+1)%7]).GetComponent<MeshRenderer>().material = ledColors[j];
			i = (i+1)%7;
			j = (j+1)%8;
			yield return new WaitForSeconds(0.12f);
		}
	}
	private IEnumerator OnSolve(){
		moduleSolved = true;
		yield return null;
		stageLights[stage].GetComponent<MeshRenderer>().material = ledColors[2];
		colorBlind[0].text = "";
		colorBlind[1].text = "";
		colorBlind[2].text = "";
		colorBlind[3].text = "";
		colorBlind[4].text = "";
		colorBlind[5].text = "";
		colorBlind[6].text = "";
		GetComponent<KMAudio>().PlaySoundAtTransform(sounds[3].name, transform);
		for(int i = 0; i<= 3; i++){
			foreach (GameObject LED in leds){LED.GetComponent<MeshRenderer>().material = ledColors[pressedColors[i]];}
			yield return new WaitForSeconds(0.4f);
			foreach (GameObject LED in leds){LED.GetComponent<MeshRenderer>().material = ledColors[8];}
			yield return new WaitForSeconds(0.4f);
		}
		for(int i = 0; i<= 31; i++){
			foreach (GameObject LED in leds){int j = UnityEngine.Random.Range(0, 8); LED.GetComponent<MeshRenderer>().material = ledColors[j];}
			yield return new WaitForSeconds(0.4f/2);
			foreach (GameObject LED in leds){LED.GetComponent<MeshRenderer>().material = ledColors[8];}
			yield return new WaitForSeconds(0.4f/2);
		}
		GetComponent<KMBombModule>().HandlePass();
		for(int i = 0; i<= 31; i++){
			foreach (GameObject LED in leds){int j = UnityEngine.Random.Range(0, 8); LED.GetComponent<MeshRenderer>().material = ledColors[j];}
			yield return new WaitForSeconds(0.4f/2);
			foreach (GameObject LED in leds){LED.GetComponent<MeshRenderer>().material = ledColors[8];}
			yield return new WaitForSeconds(0.4f/2);
		}
		StartCoroutine(Epologue());
	}
	private IEnumerator Initialize(){
		yield return null;
		if (lmID == 1) {
			GetComponent<KMAudio>().PlaySoundAtTransform(sounds[0].name, transform); //In yet another example of "I tried everything I could and still wouldn't happen", I can't make this actually play when the timer starts and not when the bomb starts. Not that it's a big issue, but it is slightly interesting. 
		}
		bool initialized = true;
		//Debug.LogFormat("[Lightmare #{0}] The digit restriction is -",moduleID);
		colorblindModeEnabled = ColorblindMode.ColorblindModeActive;
		String Serial = bomb.GetSerialNumber();
		for (int i = 0; i <= 5; i++){
			base36[5-i] = BASE36ALPHABET.IndexOf(Serial[i]);
			Debug.Log(base36[5-i]);
		}
		Debug.LogFormat("[Lightmare #{0}] Base 36 values of the Serial number in order: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, base36[5], base36[4], base36[3], base36[2], base36[1], base36[0]);
		int num36 = base36[5]*36*36*36*36*36+base36[4]*36*36*36*36+base36[3]*36*36*36+base36[2]*36*36+base36[1]*36+base36[0];
		decoded = encode(num36);
		Array.Reverse(decoded);
		order = decoded;
		Debug.LogFormat("[Lightmare #{0}] Last 6 digits of the base 7 results in order: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);
		
		for(int i = 0; i <= 5; i++){
			if (order[i] == 0){
				order[i] = 7;
			}
		}
		bool dir = (base36[0]%2 == 1);
		//Debug.Log(dir);
		//Debug.Log(order[0] + " " + order[1] + " " + order[2] + " " + order[3] + " " + order[4] + " " + order[5]);
		while(true){
			int[] check = {0, 0, 0, 0, 0, 0, 0};
			if(dir){
				for(int j = 0; j<= 5; j++){
					for(int i = 5; i >= 0; i--){
						check[order[i]-1]++;
						if(check[order[i]-1] >= 2){
							order[i] = (order[i])%7+1;
						}
					}
					for (int i = 0; i<=6; i++){
						check[i] = 0;
					}
				}
			} else {
				for(int j = 0; j<= 5; j++){
					for(int i = 0; i <= 5; i++){
						check[order[i]-1]++;
						if(check[order[i]-1] >= 2){
							order[i] = (order[i])%7+1;
						}
					}
					for (int i = 0; i<=6; i++){
						check[i] = 0;
					}
				}
			}
			break;
		}
		//Debug.Log(order[0] + " " + order[1] + " " + order[2] + " " + order[3] + " " + order[4] + " " + order[5]);
		Debug.LogFormat("[Lightmare #{0}] Order after removing duplicates: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);

		excluded = 28-order[0]-order[1]-order[2]-order[3]-order[4]-order[5]; 
		Debug.LogFormat("[Lightmare #{0}] Always connected LED is {1}.",moduleID, excluded);
		
		int[] temp = (int[])order.Clone();
		Debug.LogFormat("[Lightmare #{0}] Generating wires:",moduleID);
		for (int i = 0; i <= 6; i++){
			int a;
			a = Rnd.Range(-1, 2);
			int bottom = (i+a);
			if(bottom == -1){
				bottom = 6;
			} else if (bottom == 7){
				bottom = 0;
			}

            wires[i] = new WireInfo
            {
                TopConnector = i,
                BottomConnector = bottom,
                Level = 1

            };
			for (int j = 0; j < i; j++)
                if ((((wires[j].TopConnector > wires[i].TopConnector) && (wires[j].BottomConnector < wires[i].BottomConnector)) ||
                    ((wires[j].TopConnector < wires[i].TopConnector) && (wires[j].BottomConnector > wires[i].BottomConnector)))
					&&((((wires[j].TopConnector+4)%7 > (wires[i].TopConnector+4)%7) && ((wires[j].BottomConnector+4)%7 < (wires[i].BottomConnector+4)%7)) ||
                    (((wires[j].TopConnector+4)%7 < (wires[i].TopConnector+4)%7) && ((wires[j].BottomConnector+4)%7 > (wires[i].BottomConnector+4)%7))) 
					&&((((wires[j].TopConnector+2)%7 > (wires[i].TopConnector+2)%7) && ((wires[j].BottomConnector+2)%7 < (wires[i].BottomConnector+2)%7)) ||
                    (((wires[j].TopConnector+2)%7 < (wires[i].TopConnector+2)%7) && ((wires[j].BottomConnector+2)%7 > (wires[i].BottomConnector+2)%7)))){wires[i].Level = 2; wires[i].cross = true; anyCross = true;} //For reference of what this line does: All of the wire code is taken from Perplexing wires largely due to my own skill issue with Unity. Perplexing wires has its own way of telling if wires cross, but that runs into a large flaw with this module as it wraps around. Thus the first third of this logic statement is the perplexing wires check. The second third is Anding that withe check, but rotated by 4 places. This way, it catches more of the edge cases. The last one is +2 to catch the last of the edge cases. Related note: if anyone actually wants to explain to me how the unity rendering works, I would be open to it. 
        }
		for (int i = 0; i<=6; i++)
			for (int j = 0; j <= 6; j++)
                if ((wires[j].TopConnector > wires[i].TopConnector && wires[j].BottomConnector < wires[i].BottomConnector) ||
                    (wires[j].TopConnector < wires[i].TopConnector && wires[j].BottomConnector > wires[i].BottomConnector))wires[i].cross = true;
		var wiresParent = module.transform.Find("Wires");
		for (int wIx = 0; wIx <= 6; wIx++){
			var wireObj = wiresParent.Find("Wire" + (wIx)).gameObject;
			var topConnector = module.transform.Find("LEDs").Find("Led" + (wIx+1)).Find("Cube");
			var topControl = topConnector.Find("Control1");
			var bottomConnector = module.transform.Find("Button").Find("Connector" + (wires[wIx].BottomConnector));
        	var bottomControl = bottomConnector.Find("Control1");
        	var raiseBy = 1 * (wires[wIx].Level - 1) * (Pt)(transform.InverseTransformPoint(bottomControl.position) - transform.InverseTransformPoint(bottomConnector.position) + transform.InverseTransformPoint(topControl.position) - transform.InverseTransformPoint(topConnector.position));
			var topControlX = 0f;
        	if (wires.Any(w => w.TopConnector == wires[wIx].TopConnector && w.BottomConnector < wires[wIx].BottomConnector) && !wires.Any(w => w.TopConnector == wires[wIx].TopConnector && w.BottomConnector > wires[wIx].BottomConnector))
        	    topControlX = -.07f;
        	else if (wires.Any(w => w.TopConnector == wires[wIx].TopConnector && w.BottomConnector > wires[wIx].BottomConnector) && !wires.Any(w => w.TopConnector == wires[wIx].TopConnector && w.BottomConnector < wires[wIx].BottomConnector))
        	    topControlX = .07f;
        	var topControlZ = 0f;
        	if (wires.Any(w => Math.Sign(w.TopConnector - wires[wIx].TopConnector) != Math.Sign(w.BottomConnector - wires[wIx].BottomConnector) && w.Level < wires[wIx].Level))
        	    topControlZ = .05f;
        	else if (wires.Any(w => Math.Sign(w.TopConnector - wires[wIx].TopConnector) != Math.Sign(w.BottomConnector - wires[wIx].BottomConnector) && w.Level > wires[wIx].Level))
        	    topControlZ = -.05f;
        	topControl.localPosition = topControl.localPosition + (new Vector3(topControlX, 0.2f, topControlZ));
			var seed = Rnd.Range(0, int.MaxValue);
        	var mesh = MeshGenerator.GenerateWire(
        	    transform.InverseTransformPoint(topConnector.position),
        	    transform.InverseTransformPoint(topControl.position),
        	    transform.InverseTransformPoint(bottomControl.position),
        	    transform.InverseTransformPoint(bottomConnector.position),
        	    5,
        	    MeshGenerator.Mode.Wire,
        	    seed,
        	    raiseBy);
        	wires[wIx].MeshFilter = wireObj.GetComponent<MeshFilter>();
        	wires[wIx].MeshFilter.mesh = mesh;
			wireObj.GetComponent<MeshCollider>().sharedMesh = MeshGenerator.GenerateWire(
        	    transform.InverseTransformPoint(topConnector.position),
        	    transform.InverseTransformPoint(topControl.position),
        	    transform.InverseTransformPoint(bottomControl.position),
        	    transform.InverseTransformPoint(bottomConnector.position),
        	    8,
        	    MeshGenerator.Mode.Collider,
        	    seed,
        	    raiseBy); //Yes, I know of the slight clipping issues. I just don't know how to fix them and they don't affect gameplay. I am open to suggestions. 

        	wireObj.GetComponent<MeshCollider>().enabled = true;
        	wireObj.GetComponent<MeshCollider>().enabled = false;
		}
		//Debug.Log("Wires have been generated. ");
		Debug.LogFormat("[Lightmare #{0}] Wire generation sucessful",moduleID);

		bool swaps = false;
		//At this point in the code, it checks the initial conitions to modifiy the order. Note that it has not yet generated LED colors as they are not necessary. 
		if(anyCross){
			order[0] = temp[1];
			order[1] = temp[0];
			Debug.LogFormat("[Lightmare #{0}] Wires cross. Executing swap 1. New order: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);
			temp[0] = order[0];
			temp[1] = order[1];
			swaps = true;
		}
		for(int i = 0; i < 6; i++){
			if(Label.ToUpper().Contains(Serial[i])){
				order[0] = temp[1]; order[1] = temp[0]; order[2] = temp[3]; order[3] = temp[2]; order[4] = temp[5]; order[5] = temp[4];
				temp = (int[])order.Clone();
				Debug.LogFormat("[Lightmare #{0}] Button label shares a value with the Serial Number. Executing swap 2. New order: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);
				swaps = true;
				break;
			}
		}
		if(buttonColor == 1 && pattern == 0){
			for(int i = 0; i<= 5; i++){
				order[i] = temp[(i-base36[5]+36)%6];
			}
			temp = (int[])order.Clone();
			Debug.LogFormat("[Lightmare #{0}] Pattern is checkers and button color is blue. Executing swap 3. New order: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);
			swaps = true;
		}
		if(shape == 2 && buttonColor != 3 && buttonColor != 5 && buttonColor != 6){
			//Debug.Log("Square and not secondary. Executing swap 4");
			for(int i = 0; i<= 5; i++){
				order[i] = temp[(i+base36[0])%6];
			}
			temp = (int[])order.Clone();
			//Debug.Log(order[0] + " " + order[1] + " " + order[2] + " " + order[3] + " " + order[4] + " " + order[5]);
			swaps = true;
			Debug.LogFormat("[Lightmare #{0}] Button Shape is Square and Button Color is not secondary. Executing swap 4. New order: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);


		}

		String Lightmare = "L1GHTM4R3";
		bool labelCommon = false;
		bool serialCommon = false;
		for(int i = 0; i < 9; i++){
			if(Label.ToUpper().Contains(Lightmare[i])){
				labelCommon = true;
			}
			if(Serial.Contains(Lightmare[i])){
				serialCommon = true;
			}
		}
		if(labelCommon ^ serialCommon){
			//Debug.Log("Button XOR Serial shares a character with L1GHTM4R3. Performing swap 5");
			order[0] = temp[5];
			order[5] = temp[0];
			temp = (int[])order.Clone();
			//Debug.Log(order[0] + " " + order[1] + " " + order[2] + " " + order[3] + " " + order[4] + " " + order[5]);
			Debug.LogFormat("[Lightmare #{0}] Button XOR Serial shares a character with L1GHTM4R3. Executing swap 5. New order: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);
			swaps = true;
		}
		StartCoroutine(UpdateLED(excluded)); //This is where the code actually calculates the colors for the always connected bulb, as it is needed now and there is no way that it can softlock at this point. All the rest of the calculations will be saved for later. 
		excluded -= 1;
		yield return new WaitForSeconds(0.00001f);
		//Debug.Log(ledFlashes[excluded, 4]);
		if(pattern == 1 &&(buttonColor == ledFlashes[excluded, 1] || buttonColor == ledFlashes[excluded, 2] ||buttonColor == ledFlashes[excluded, 3] ||buttonColor == ledFlashes[excluded, 4])){
			//Debug.Log("Pattern is Cube Net and button shares a color with unused. Executing swap 6");
			Array.Reverse(order);
			Array.Reverse(temp);
			//Debug.Log(order[0] + " " + order[1] + " " + order[2] + " " + order[3] + " " + order[4] + " " + order[5]);
			Debug.LogFormat("[Lightmare #{0}] Pattern is Cube Net and Button Color is in always connected bulb. Executing swap 6. New order: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);
			swaps = true;
		}
		if(swaps == false){
			Debug.LogFormat("[Lightmare #{0}] No previous conditions applied. Executing swap 7. ", moduleID);
			
			order[0] = temp[2]; order[1] = temp[0]; order[2] = temp[1]; order[3] = temp[4]; order[4] = temp[5]; order[5] = temp[3];
		}
		//Debug.Log("Final Order:" + order[0] + " " + order[1] + " " + order[2] + " " + order[3] + " " + order[4] + " " + order[5]);
		Debug.LogFormat("[Lightmare #{0}] Final order after all swaps: {1}, {2}, {3}, {4}, {5}, {6}. ",moduleID, order[0], order[1], order[2], order[3], order[4], order[5]);
		StartCoroutine(UpdateLED(order[5]));
		StartCoroutine(UpdateLED(order[4]));
		yield return new WaitForSeconds(0.00001f);
		int unused1 = -1;
		int unused2 = -1;
		if(ledFlashes[order[4]-1, 4] == 1 || ledFlashes[order[4]-1, 4] == 2 || ledFlashes[order[4]-1, 4] == 4){
			unused1 = primary(order[4]);
			Debug.LogFormat("[Lightmare #{0}] First unused bulb's last color is primary. Doing those rules.",moduleID);
		} else if(ledFlashes[order[4]-1, 4] == 3 || ledFlashes[order[4]-1, 4] == 5 || ledFlashes[order[4]-1, 4] == 6){
			Debug.LogFormat("[Lightmare #{0}] First unused bulb's last color is secondary. Doing those rules.",moduleID);
			unused1 = secondary(order[4]);
		} else {
			Debug.LogFormat("[Lightmare #{0}] First unused bulb's last color is white/black. Doing those rules.",moduleID);
			unused1 = kwFunction(order[4]);
		}
		yield return new WaitForSeconds(0.00001f);
		if(ledFlashes[order[5]-1, 4] == 1 || ledFlashes[order[5]-1, 4] == 2 || ledFlashes[order[5]-1, 4] == 4){
			Debug.LogFormat("[Lightmare #{0}] Second unused bulb's last color is primary. Doing those rules.",moduleID);
			unused2 = primary(order[5]);
		} else if(ledFlashes[order[5]-1, 4] == 3 || ledFlashes[order[5]-1, 4] == 5 || ledFlashes[order[5]-1, 4] == 6){
			Debug.LogFormat("[Lightmare #{0}] Second unused bulb's last color is secondary. Doing those rules.",moduleID);
			unused2 = secondary(order[5]);
		} else{
			Debug.LogFormat("[Lightmare #{0}] Second unused bulb's last color is white/black. Doing those rules.",moduleID);
			unused2 = kwFunction(order[5]);
		}
		//Debug.Log(unused1 + " " + unused2);
		finalColor = unused1 ^ unused2;
		Debug.LogFormat("[Lightmare #{0}] The first color obtained from the rules is {1}. The second color obtained from the rules is {2}. The result of XORing them is {3}, which is the final color and the general restriction. ",moduleID, NumToCol(unused1), NumToCol(unused2), NumToCol(finalColor));
		int[] colorStartNumber = {11, 11, 16, 12, 27, 22, 34, 32};
		if(base36.Contains(colorStartNumber[ledFlashes[excluded, 1]]) || base36.Contains(colorStartNumber[ledFlashes[excluded, 2]]) || base36.Contains(colorStartNumber[ledFlashes[excluded, 3]]) || base36.Contains(colorStartNumber[ledFlashes[excluded, 4]])){
			startingColor = (~((ledFlashes[excluded, 1] ^ ledFlashes[excluded, 3])^(ledFlashes[excluded, 2] ^ ledFlashes[excluded, 4]))^~(7));
			Debug.LogFormat("[Lightmare #{0}] The Serial Number shares a first letter with one of the colors in the excluded. XNORing the two XORs. ",moduleID);
		} else {
			startingColor = ledFlashes[excluded, 1] ^ ledFlashes[excluded, 2] ^ ledFlashes[excluded, 3] ^ ledFlashes[excluded, 4];
			Debug.LogFormat("[Lightmare #{0}] The Serial Number does not share a first letter with one of the colors in the excluded. XORing the two XORs. ",moduleID);
		}
		//Debug.Log(startingColor);
		Debug.LogFormat("[Lightmare #{0}] The starting color is {1}. ",moduleID, NumToCol(startingColor));
		if(startingColor == finalColor){
			startingColor = 7-startingColor;
			Debug.LogFormat("[Lightmare #{0}] Starting Color is the same as the final color. Inverting. New final color: {1}. ",moduleID, NumToCol(startingColor));
		}
		int trackedColor = startingColor; //This part of the code is generating at least one possible way through it. I don't think it would be that likely to get an impossible position organically, but it is theoretically possible, so this is here. 
		for(int i = 0; i<= 2; i++){
			int candidate = UnityEngine.Random.Range(1, 5);
			int candidateColor = UnityEngine.Random.Range(0, 8);
			if(candidateColor == finalColor || candidateColor == ledFlashes[excluded, i+1]){//A lot of this has special cases for LED 1 because the Primary Color function has a chance to set its first flash. 
				i--;
				continue;
			}
			if(ledFlashes[order[i]-1, candidate] != -1 || (order[i]-1 == 0 && ledFlashes[0, 1] == candidateColor) || (order[i]-1 == 0 && ledFlashes[0, 1] != -1 && candidate == 1)){
				i--;
				continue;
			}
			if(i<=1){
				trackedColor = trackedColor ^ candidateColor;
				chosenIndex[i]  = candidate;
				ledFlashes[order[i] - 1, candidate] = candidateColor;
				continue;
			}
			if((((candidateColor ^ trackedColor) ^ finalColor) == finalColor) || (((candidateColor ^ trackedColor) ^ finalColor) == ledFlashes[excluded, 4])){
				i--;
				continue;
			}
			if(order[3] == 1 && ledFlashes[0, 1] != -1){
				if(((candidateColor ^ trackedColor) ^ finalColor) == ledFlashes[0, 1]){
					chosenIndex[3] = 1;
				} else {
					chosenIndex[3] = UnityEngine.Random.Range(2, 5);
				}
			} else {
				chosenIndex[3] = UnityEngine.Random.Range(1, 5);
			}
			chosenIndex[2] = candidate;
			ledFlashes[order[2]-1, chosenIndex[2]] = candidateColor;
			ledFlashes[order[3]-1, chosenIndex[3]] = (candidateColor ^ trackedColor) ^ finalColor;
		}
		StartCoroutine(UpdateLED(order[3]));
		StartCoroutine(UpdateLED(order[2]));
		StartCoroutine(UpdateLED(order[1]));
		StartCoroutine(UpdateLED(order[0]));
		yield return new WaitForSeconds(0.00001f);
		for(int i = 1; i <= 7; i++){
			Debug.LogFormat("[Lightmare #{0}] LED {1} is {2}, {3}, {4}, {5}.", moduleID, i, NumToCol(ledFlashes[i-1, 1]), NumToCol(ledFlashes[i-1, 2]), NumToCol(ledFlashes[i-1, 3]),NumToCol(ledFlashes[i-1, 4]));
		}
		Debug.LogFormat("[Lightmare #{0}] A possible solution is {1}, {2}, {3}, {4} displayed on the LEDs that are in the order.", moduleID, NumToCol(ledFlashes[order[0]-1, chosenIndex[0]]), NumToCol(ledFlashes[order[1]-1, chosenIndex[1]]), NumToCol(ledFlashes[order[2]-1, chosenIndex[2]]),NumToCol(ledFlashes[order[3]-1, chosenIndex[3]]));
		initialized = true;
		currentColor = startingColor;
		yield return new WaitForSeconds(1);
		lmCounter = 1;
	}
	// Update is called once per frame

	IEnumerator ProcessTwitchCommand (string Command){
        Command = Command.Trim().ToUpper();
        yield return null;
        string[] Commands = Command.Split(' ');
		if (Commands.Length == 1 && (Commands[0] == "COLORBLIND" || Commands[0] == "COLOURBLIND")){
			colorblindModeEnabled = true;
			yield break;
		}
        if (!"1234567".Contains(Commands[0][0]) || Commands[0].Length != 1){
            yield return "sendtochaterror Invalid command. The first statement must be a number from 1-7. ";
            yield break;
        } else if (!"1234KBGCRMYW".Contains(Commands[1][0]) || Commands[0].Length != 1){
            yield return "sendtochaterror Invalid Command. The second statement must be either a number from 1-4 or the letter of a color. ";
            yield break;
        } else if(Commands.Length != 2){
			yield return "sendtochaterror Invalid Command. There must be 2 characters in the command. ";
            yield break;
		}
		string indeces = "KBGCRMYW";
		int choson = indeces.IndexOf(Commands[1][0]);
		if(stage == 3){
			yield return "solve";
			yield return "strike";
		}
		if("1234".Contains(Commands[1])){
			while(ledStatus[int.Parse(Commands[0])-1] != int.Parse(Commands[1])){
				yield return new WaitForSeconds(0.05f);
			}
			ButtonPress(selectable);
			yield break;
		}
		int i = 0;
		while(ledFlashes[int.Parse(Commands[0])-1, ledStatus[int.Parse(Commands[0])-1]] != choson){
			yield return new WaitForSeconds(0.05f);
			if(i >= 70){
				yield return "That color doesn't seem to be on that LED. Stopping execution. ";
				yield break;
			}
			i++;
		}
		ButtonPress(selectable);
		yield break;

    }
	private IEnumerator ChooseMeshes(){
		shape = UnityEngine.Random.Range(0, 11);
		String[] ShapePossibilities = {"Circle", "Triangle", "Square", "Rectangle", "Pentagon", "Hexagon", "Octagon", "Trapezoid", "Oval", "Star", "Heptagon"};
		Button.GetComponent<MeshFilter>().mesh = shapes[shape];
		Highlight.GetComponent<MeshFilter>().mesh = shapes[shape];
		Debug.LogFormat("[Lightmare #{0}] Successfully generated button shape. Shape: {1}.", moduleID, ShapePossibilities[shape]);

		pattern = UnityEngine.Random.Range(0, 7);
		String[] PatternPossibilities = {"Checkers", "Cube Net", "Dodecahedron Net", "Finger Print", "Spiral", "Tic Tac Toe", "Triforce"};
		module.transform.Find("Button").Find("Button").Find("Pattern").GetComponent<SpriteRenderer>().sprite = patterns[pattern];
		Debug.LogFormat("[Lightmare #{0}] Successfully generated pattern. Pattern: {1}.", moduleID, PatternPossibilities[pattern]);
		buttonColor = UnityEngine.Random.Range(1, 8); //Can't be black, the pattern would be invisible. 
		Debug.LogFormat("[Lightmare #{0}] Successfully generated button color. Button Color: {1}.", moduleID, NumToCol(buttonColor));
		Button.GetComponent<MeshRenderer>().material = buttonColors[buttonColor];
		if(buttonColor >= 6 || buttonColor == 3){
			Display.GetComponent<TextMesh>().color = new Color(0, 0, 0);
		}

		labelIndex = UnityEngine.Random.Range(0, DISPLAYS.Length);
		Label = DISPLAYS[labelIndex];
		Display.text = Label;
		Debug.LogFormat("[Lightmare #{0}] Successfully generated button label. Button Label: {1}.", moduleID, DISPLAYS[labelIndex]);
		yield return null;
	}
	private IEnumerator UpdateLED(int index){
		yield return null;
		index -= 1;
		ledFlashes[index, 0] = 8;
		for(int i = 1; i <= 4; i++){
			if(ledFlashes[index, i] == -1){
				ledFlashes[index, i] = UnityEngine.Random.Range(0, 8);
			}
			if((ledFlashes[index, 1] == ledFlashes[index, i] && i != 1) ||(ledFlashes[index, 2] == ledFlashes[index, i] && i != 2) || (ledFlashes[index, 3] == ledFlashes[index, i] && i != 3)|| (ledFlashes[index, 4] == ledFlashes[index, i] && i != 4)){
				ledFlashes[index, i] = -1;
				i--;
			}
		}
		ledStatus[index] = 0;
		float time = UnityEngine.Random.Range(0.4f, 0.65f);
		yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.4f));
		string[] colorblindColors = {"K", "B", "G", "C", "R", "M", "Y", "W", ""};
		while(moduleSolved != true){
			leds[index].GetComponent<MeshRenderer>().material = ledColors[ledFlashes[index, ledStatus[index]]];
			if(colorblindModeEnabled){
				colorBlind[index].text = colorblindColors[ledFlashes[index, ledStatus[index]]];
			}
			yield return new WaitForSeconds((time-0.15f));
			if(moduleSolved == true){
				colorBlind[index].text = "";
				break;
			}
			leds[index].GetComponent<MeshRenderer>().material = ledColors[8];
			colorBlind[index].text = "";
			yield return new WaitForSeconds(0.15f);
			ledStatus[index] = (ledStatus[index]+1)%5;
		}
		
	}
	public int primary(int index){
		index -= 1;
		Debug.Log(ledFlashes[index, 1] + " " + ledFlashes[index, 2] + " " + ledFlashes[index, 3] + " " + ledFlashes[index, 4]);
		Debug.Log(buttonColor);
		for(int i = 1; i<=4; i++){
			if(ledFlashes[index, i] == buttonColor){
				return 4;
			}
		}
		if(ledFlashes[0, 1] == -1){
			ledFlashes[0, 1] = UnityEngine.Random.Range(0, 8);
			Debug.Log(ledFlashes[0, 1]);
		}
		if(ledFlashes[index, 2] == ledFlashes[0, 1]) return 6;
		if(ledFlashes[index, 1] == 0) return 7;
		for(int i = 1; i<=4; i++){
			if(ledFlashes[index, i] == 7){
				return 0;
			}
		}
		foreach (IEnumerable<string> currentPlate in bomb.GetPortPlates().Where(a => a.Contains("RJ45"))){
			if( currentPlate.Contains("PS2")){
				return 2;
			}
		}
		if(bomb.IsIndicatorOn("BOB")){
			return 5;
		}
		if(order[4] == 7 || order[5] == 7){
			return 1;
		}
		return 3;
	}

	public int secondary(int index){
		index -= 1;

		bool lit = false;
		bool unlit = false;
		foreach(string ind in bomb.GetIndicators()){
			for(int i = 0; i< ind.Length; i++){
				if(Label.ToUpper().Contains(ind[i])){
					if(bomb.IsIndicatorOff(ind)){
						unlit = true;
					} else {
						lit = true;
					}
				}
			}
		}
		if(lit){
			return 1;
		}
		if(unlit){
			return 6;
		}
		if(ledFlashes[index, 3] == buttonColor){
			return 3;
		}
		if(pattern == 0 || pattern == 4 || pattern == 5){
			return 0;
		}
		if(shape == 2 || shape == 3 || shape == 7){
			return 4;
		}
		if(shape == 9){
			return 5;
		}
		if(buttonColor != 1 && buttonColor != 2 && buttonColor != 4){
			return 7;
		}
		return 2;
	}
	public int kwFunction(int index){
		index -= 1;
		int[] secondaries = {3, 5, 6};
		if(!(secondaries.Contains(buttonColor))){
			return 2;
		}
		if(ledFlashes[order[4]-1, 3] == ledFlashes[order[5]-1, 3]){
			return 4;
		}
		if(bomb.GetBatteryCount() % 2 == 0 && bomb.GetBatteryHolderCount() % 2 == 0){
			return 0;
		}
		int shared = 0;
		for(int i = 1; i<=4; i++){
			for(int j = 1; j<=4; j++){
				if(ledFlashes[order[4]-1, i] == ledFlashes[order[5]-1, j]) {
					shared++;
				}
			}
		}
		if(shared >= 2){
			return 1;
		}
		if(Math.Abs(excluded-index) == 1 || Math.Abs(excluded-index) == 6){
			return 3;
		}
		if(!base36.Contains(10) && !base36.Contains(14) && !base36.Contains(18) && !base36.Contains(24) && !base36.Contains(30)){
			return 6;
		}
		if(!anyCross){
			return 5;
		}
		return 7;
	}
	String NumToCol(int i ){
		//Most of the time throughout the code, it refers to colors as simply numbers. Take this guide as a referance.
		switch(i){
			case 0: return "Black";
			case 1: return "Blue";
			case 2: return "Green";
			case 3: return "Cyan";
			case 4: return "Red";
			case 5: return "Magenta";
			case 6: return "Yellow";
			case 7: return "White";
			case 8: return "Off";
			default: return "Error";
		}
	}
	public static int[] encode(long result){
    	int[] array = {0, 0, 0, 0, 0, 0};
    	while(true){
    	    if(result <= 0){
    	        break;
    	    }
    	    int n=0;
    	    while((long)Math.Pow(7, n+1)<=result){
    	        n++;
    	    }
    	    if (n <= array.Length-1){
    	        array[n] = array[n]+1;
    	    }
    	    result = result-(long)Math.Pow(7, n);
    	}
    	return array;
    }
	#pragma warning disable 414
        private readonly string TwitchHelpMessage = @"Press at a certain step in the cycle by doing !{0} 1 2 to press when the first LED as described in the manual is in the second part of its sequence. Use !{0} 1 M to press when LED1 is displaying Magenta. Valid colors are KRGBCMYW. Do !{0} Colorblind for colorblind support. ";
    #pragma warning restore 414
	IEnumerator TwitchHandleForcedSolve () { //Note: The way that this solver works is that it disables the restrictions to submit. 
        yield return null;
        onStrike();
		for(int i = 0; i <= 3; i++){
			while(ledStatus[order[i] - 1] != chosenIndex[i]){
				yield return new WaitForSeconds(0.05f);
			}
			ButtonPress(selectable);
		}

    }
}