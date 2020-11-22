using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;

public class TranspositionScript : MonoBehaviour
{
	public KMBombInfo Bomb;
	public KMAudio Audio;
	public KMBombModule Module;
	
	public KMSelectable[] Key;
	public KMSelectable Submit, Reset;
	public TextMesh ScreenText, ScreenText2;
	public AudioClip[] PianoSounds;
	public AudioClip Victory;
	
	bool Animating = false;
	int Baseline = 0, Keyboard = 0;
	int[] Notes = new int[6], CorrectAnswers = new int[6], Input = {-1, -1, -1, -1, -1, -1};
	string[] NoteTerms = {"C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"};
	
	//Logging
	static int moduleIdCounter = 1;
	int moduleId;
	private bool ModuleSolved;
	
	int[][] Transposition = new int[][]{
		new int[] {4, -1, -2, 4},
		new int[] {2, 3, -1, -1},
		new int[] {-4, 4, -3, -2},
		new int[] {1, 2, 0, 3},
		new int[] {0, -3, -4, 1},
		new int[] {3, -2, -2, 1},
		new int[] {-4, 0, -3, 2},
		new int[] {3, -3, 0, 1},
		new int[] {-1, -1, -4, 2},
		new int[] {0, 3, -1, 4},
		new int[] {-3, 1, 2, 4},
		new int[] {2, 1, -4, -2},
		new int[] {-1, 0, -3, 4},
		new int[] {3, -1, -3, -3},
		new int[] {-2, 3, -4, 0},
		new int[] {1, 4, 0, 2},
		new int[] {3, -2, -2, 4},
		new int[] {-4, -1, 1, 2},
		new int[] {4, -1, 2, -3},
		new int[] {3, 0, -4, 1}
	};	
	
	void Start()
	{
		Init();
	}
	
	void Awake()
	{
		moduleId = moduleIdCounter++;
		Submit.OnInteract += delegate(){CheckAnswer(); return false;};
		Reset.OnInteract += delegate(){ResetAnswer(); return false;};
		for (int i = 0; i < 12; i++)
		{
			int j = i;
			Key[i].OnInteract += delegate ()
			{
				handlePress(j);
				return false;
			};
		}
	}
	
	void Init()
	{
		string[] MusicalTerms = {"P", "R", "I", "RI"};
		for (int x = 0; x < 6; x++)
		{
			Notes[x] = UnityEngine.Random.Range(0, PianoSounds.Length);
			ScreenText.text += x != 5 ? NoteTerms[Notes[x]]+ " " : PianoSounds[Notes[x]].name;
		}
		ScreenText2.text = MusicalTerms[UnityEngine.Random.Range(0, MusicalTerms.Length)];
		Debug.LogFormat("[Musical Transposition #{0}] This module has 6 notes, being {1}, {2}, {3}, {4}, {5}, {6}, and a modifier of {7}.", moduleId, NoteTerms[Notes[0]], NoteTerms[Notes[1]], NoteTerms[Notes[2]], NoteTerms[Notes[3]], NoteTerms[Notes[4]], NoteTerms[Notes[5]], ScreenText2.text);
		
		int StartingGuide = Bomb.GetSerialNumberNumbers().Last();
		Debug.LogFormat("[Musical Transposition #{0}] The starting number starts with {1}.", moduleId, Bomb.GetSerialNumberNumbers().Last().ToString());
		if (new[] {'A', 'E', 'I', 'O', 'U'}.Any(c => Bomb.GetSerialNumber().Contains(c)))
		{
			StartingGuide += 3;
			Debug.LogFormat("[Musical Transposition #{0}] The serial number contains a vowel. The number is now equal to {1}.", moduleId, StartingGuide);
		}
		StartingGuide -= Bomb.GetBatteryHolderCount();
		Debug.LogFormat("[Musical Transposition #{0}] After subtracting the amount of battery holders to the starting number, the number is now equal to {1}.", moduleId, StartingGuide);
		if (!Bomb.IsIndicatorOn("FRK") && Bomb.GetOnIndicators().Count() >= 2)
		{
			StartingGuide += 2;
			Debug.LogFormat("[Musical Transposition #{0}] There was no lit FRK, and the lit indicator count is {1}. The number is now equal to {2}", moduleId, Bomb.GetOnIndicators().Count(), StartingGuide);
		}
		if (!Bomb.IsIndicatorOff("CAR") && Bomb.GetOffIndicators().Count() >= 2)
		{
			StartingGuide -= 2;
			Debug.LogFormat("[Musical Transposition #{0}] There was no unlit CAR, and the unlit indicator count is {1}. The number is now equal to {2}", moduleId, Bomb.GetOnIndicators().Count(), StartingGuide);
		}
		if (Bomb.IsPortPresent("StereoRCA"))
		{
			StartingGuide -= 4;
			Debug.LogFormat("[Musical Transposition #{0}] Stereo RCA is present. The number is now equal to {1}", moduleId, StartingGuide);
		}
		if (Bomb.IsPortPresent("Serial"))
		{
			StartingGuide *= 2;
			Debug.LogFormat("[Musical Transposition #{0}] Serial is present. The number is now equal to {1}", moduleId, StartingGuide);
		}
		
		if (StartingGuide < 1)
		{
			do
			{
				StartingGuide += 5;
			}
			while (StartingGuide < 1);
		}
		
		else if (StartingGuide > 20)
		{
			do
			{
				StartingGuide -= 5;
			}
			while (StartingGuide > 20);
		}
		
		Debug.LogFormat("[Musical Transposition #{0}] After adjustments, the number is now equal to {1}", moduleId, StartingGuide);
		Baseline = Bomb.GetSerialNumberNumbers().Last() % 2 == 0 ? 0 : 2;
		
		if (Baseline == 0)
		{
			if (!Bomb.IsPortPresent("Parallel"))
			{
				Debug.LogFormat("[Musical Transposition #{0}] Last serial digit is even", moduleId);
				Baseline += 1;
			}
			
			else
			{
				Debug.LogFormat("[Musical Transposition #{0}] Last serial digit is even, and there is a Parallel port", moduleId);
			}
		}
		
		else if (Baseline == 2)
		{
			if (!Bomb.IsPortPresent("RJ45"))
			{
				Debug.LogFormat("[Musical Transposition #{0}] Last serial digit is odd", moduleId);
				Baseline += 1;
			}
			
			else
			{
				Debug.LogFormat("[Musical Transposition #{0}] Last serial digit is odd, and there is a RJ-45 port", moduleId);
			}
		}
		
		Debug.LogFormat("[Musical Transposition #{0}] The correct transposition for the module is {1}", moduleId, Transposition[StartingGuide - 1][Baseline].ToString());
		
		for (int a = 0; a < 6; a++)
		{
			switch (ScreenText2.text)
			{
				case "P":
					CorrectAnswers[a] = (12 + Notes[a] + Transposition[StartingGuide - 1][Baseline]) % 12;
					break;
				case "R":
					CorrectAnswers[5 - a] = (12 + Notes[a] + Transposition[StartingGuide - 1][Baseline]) % 12;
					break;
				case "I":
					CorrectAnswers[a] = (12 + Notes[0] + (-1 * (Notes[a] - Notes[0])) + Transposition[StartingGuide - 1][Baseline]) % 12;
					break;
				case "RI":
					CorrectAnswers[5 - a] = (12 + Notes[0] + (-1 * (Notes[a] - Notes[0])) + Transposition[StartingGuide - 1][Baseline]) % 12;
					break;
				default:
					break;
			}
		}
		
		switch (ScreenText2.text)
		{
			case "P":
				Debug.LogFormat("[Musical Transposition #{0}] The modifier was P, so all notes were only transposed by {1} semitones, resulting in sequence {2} {3} {4} {5} {6} {7}.",moduleId, Transposition[StartingGuide - 1][Baseline].ToString(), NoteTerms[CorrectAnswers[0]], NoteTerms[CorrectAnswers[1]], NoteTerms[CorrectAnswers[2]], NoteTerms[CorrectAnswers[3]], NoteTerms[CorrectAnswers[4]], NoteTerms[CorrectAnswers[5]]);
				break;
			case "R":
				Debug.LogFormat("[Musical Transposition #{0}] The modifier was R, so all notes were reversed and transposed by {1} semitones, resulting in sequence {2} {3} {4} {5} {6} {7}.",moduleId, Transposition[StartingGuide - 1][Baseline].ToString(), NoteTerms[CorrectAnswers[0]], NoteTerms[CorrectAnswers[1]], NoteTerms[CorrectAnswers[2]], NoteTerms[CorrectAnswers[3]], NoteTerms[CorrectAnswers[4]], NoteTerms[CorrectAnswers[5]]);
				break;
			case "I":
				Debug.LogFormat("[Musical Transposition #{0}] The modifier was I, so all difference in semitone from the first note were inverted and transposed by {1} semitones, resulting in sequence {2} {3} {4} {5} {6} {7}.",moduleId, Transposition[StartingGuide - 1][Baseline].ToString(), NoteTerms[CorrectAnswers[0]], NoteTerms[CorrectAnswers[1]], NoteTerms[CorrectAnswers[2]], NoteTerms[CorrectAnswers[3]], NoteTerms[CorrectAnswers[4]], NoteTerms[CorrectAnswers[5]]);
				break;
			case "RI":
				Debug.LogFormat("[Musical Transposition #{0}] The modifier was RI, so all difference in semitone from the first note were inverted, then the sequence was reversed and transposed by {1} semitones, resulting in sequence {2} {3} {4} {5} {6} {7}.",moduleId, Transposition[StartingGuide - 1][Baseline].ToString(), NoteTerms[CorrectAnswers[0]], NoteTerms[CorrectAnswers[1]], NoteTerms[CorrectAnswers[2]], NoteTerms[CorrectAnswers[3]], NoteTerms[CorrectAnswers[4]], NoteTerms[CorrectAnswers[5]]);
				break;
			default:
				break;
		}
	}

	public void handlePress(int num)
	{
		StartCoroutine(DoKeyAnimation(num));
		if (!ModuleSolved)
		{
			if (Keyboard != 6)
			{
				Input[Keyboard] = num;
				Keyboard++;
			}
			Audio.PlaySoundAtTransform(PianoSounds[num].name, transform);
		}
	}
	
	private IEnumerator DoKeyAnimation(int keyNumber)
	{
		yield return new WaitUntil(() => !Animating);

		Animating = true;
		
		// Black key and White key use different positions / angles
		var dy = new[] {1, 3, 6, 8, 10}.Contains(keyNumber) ? -0.03f : -0.07f; 
		var dax = new[] {1, 3, 6, 8, 10}.Contains(keyNumber) ? 5f : 0.5f;

		// Both of the following variables are per-direction.
		var frames = 5;
		var duration = .05f;

		for (var i = 0; i < frames * 2; i++)
		{
			var x = Key[keyNumber].transform.localPosition.x;
			var y = Key[keyNumber].transform.localPosition.y;
			var z = Key[keyNumber].transform.localPosition.z;
			Key[keyNumber].transform.localPosition = new Vector3(x, y + (dy / frames) * (i >= frames ? -1 : 1), z);

			var ax = Key[keyNumber].transform.localEulerAngles.x;
			var ay = Key[keyNumber].transform.localEulerAngles.y;
			var az = Key[keyNumber].transform.localEulerAngles.z;
			// Due to the way I modeled the keys, the 3rd, 5th, 10th, and 12th, key need to be rotated the other way.
			Key[keyNumber].transform.localEulerAngles = new Vector3(ax + (dax / frames) * (i >= frames ? -1 : 1) * (new[] {2, 4, 9, 11}.Contains(keyNumber) ? -1 : 1), ay, az);
			
			yield return new WaitForSeconds(duration/frames);
		}

		Animating = false;
	}
	
	void CheckAnswer()
	{
		Submit.AddInteractionPunch(.2f);
		if (!ModuleSolved)
		{
			for (int x = 0; x < 6; x++)
			{
				if (Input[x] != CorrectAnswers[x])
				{
					Module.HandleStrike();
					return;
				}
			}

			Module.HandlePass();
			ModuleSolved = true;
			ScreenText.text = "MODULE SOLVED";
			Audio.PlaySoundAtTransform(Victory.name, transform);
		}
	}
	
	void ResetAnswer()
	{
		if (!ModuleSolved)
		{
			Reset.AddInteractionPunch(.2f);
			Input = new int[] {-1, -1, -1, -1, -1, -1};
			Keyboard = 0;
		}
	}
	
	//twitch plays
    #pragma warning disable 414
	private readonly string TwitchHelpMessage = @"To press the keys on the piano, use the command !{0} <6 key notes> (They must be separated by one space) | To reset the inputs of the module, use the command !{0} reset | To submit the inputs given, use the command !{0} submit";
    #pragma warning restore 414
	
	IEnumerator ProcessTwitchCommand(string command)
    {
		string[] parameters = command.Split(' ');
		if (Regex.IsMatch(command, @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            Submit.OnInteract();
        }
		
		else if (Regex.IsMatch(command, @"^\s*reset\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            Reset.OnInteract();
        }
		
		else
		{
			yield return null;
			if (parameters.Length != 6)
			{
				yield return "sendtochaterror Invalid parameter length. The command was not processed.";
				yield break;
			}
			
			for (int x = 0; x < parameters.Length; x++)
			{
				if (!parameters[x].EqualsAny(NoteTerms))
				{
					yield return "sendtochaterror One or more key being submitted is not valid. The command was not processed.";
					yield break;
				}
			}
			
			for (int x = 0; x < parameters.Length; x++)
			{
				Key[Array.IndexOf(NoteTerms, parameters[x])].OnInteract();
				yield return new WaitForSecondsRealtime(0.5f);
			}
		}
	}
}