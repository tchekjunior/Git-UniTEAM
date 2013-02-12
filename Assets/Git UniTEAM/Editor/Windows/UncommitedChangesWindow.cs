using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using LibGit2Sharp;
using LibGit2Sharp.Core;
using LibGit2Sharp.Handlers;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace UniTEAM {
	public class UncommitedChangesWindow {

		
		private List<string> pathNodes = new List<string>();
		private Dictionary<string, bool> checkboxValues = new Dictionary<string, bool>();
		private Dictionary<string, bool> foldoutValues = new Dictionary<string, bool>();
		private GUIStyle statusStyle;
		private GUIStyle highlightStyle;
		private GUIStyle noStyle;

		private Texture2D highlightTexture;
		private Texture2D noTexture;

		public TreeChanges changes;
		public IEnumerable<string> untracked; 
		public static Rect rect;
		private Vector2 scroll;
		private string commitText = string.Empty;

		public UncommitedChangesWindow(  ) {

			highlightTexture = getGenericTexture( 1, 1, new Color( 71f / 255f, 71f / 255f, 71f / 255f ) );
			noTexture = getGenericTexture( 1, 1, new Color( 46f / 255f, 46f / 255f, 46f / 255f ) );

			statusStyle = new GUIStyle( "Label" );
			statusStyle.alignment = TextAnchor.LowerRight;

			highlightStyle = new GUIStyle( "Label" );
			highlightStyle.normal.background = highlightTexture;

			noStyle = new GUIStyle( "Label" );
			noStyle.normal.background = noTexture;
		}

		public void reset(TreeChanges newChanges, Console console) {
			changes = newChanges;
			untracked = console.repo.Index.RetrieveStatus().Untracked;

			//# If anything evaluates true here, this means someone is currently working in the commit window, and
			//# we don't want to interrupt their changes.
			if ( checkboxValues.ContainsValue( false ) || foldoutValues.ContainsValue( false ) || commitText.Trim().Length > 0 ) {
				return;
			}

			checkboxValues.Clear();
			foldoutValues.Clear();
		}

		public void draw(Console console, int i ) {
			bool highlight = true;
			pathNodes.Clear();

			scroll = GUILayout.BeginScrollView( scroll );

			changes = changes ?? console.repo.Diff.Compare();

			
			foreach ( TreeEntryChanges change in changes ) {
				GUI.enabled = true;
				recurseToAssetFolder( change, ref highlight );
			}

			/*foreach ( string untrackedFile in untracked ) {
				recurseToAssetFolder( untrackedFile, ref highlight );
			}*/

			GUI.enabled = true;
			GUILayout.EndScrollView();

			GUILayout.Label( "Commit message:" );
			commitText = GUILayout.TextArea( commitText );
			if ( GUILayout.Button( "Commit Changes" ) ) {
				Signature signature = new Signature( "Jerome Doby", "xaerodegreaz@gmail.com", System.DateTimeOffset.Now );

				//# Stage everything
				string[] stage = new string[checkboxValues.Count];

				i = 0;
				foreach ( KeyValuePair<string, bool> pair in checkboxValues ) {
					if ( pair.Value ) {
						stage[ i ] = pair.Key;
						i++;
					}
				}

				stage = stage.Where( x => !string.IsNullOrEmpty( x ) ).ToArray();

				if ( stage.Length == 0 ) {
					Console.currentError = "You cannot commit without staged items.";
					Console.currentErrorLocation = rect;
				}else if(commitText.Equals( string.Empty )) {
					Console.currentError = "Please enter a commit message.";
					Console.currentErrorLocation = rect;
				} else {
					console.repo.Index.Stage( stage );
					console.repo.Commit( commitText, signature );
					console.fetch();
				}

				commitText = string.Empty;
			}
			
		}

		private void recurseToAssetFolder( TreeEntryChanges change, ref bool highlight) {
			int spacing = 20;
			bool iterationIsDir = false;
			string[] pathArray = change.Path.Split( "\\".ToCharArray() );

			for ( int i = 0; i < pathArray.Length; i++ ) {

				if ( pathNodes.Contains( pathArray[ i ] ) || !GUI.enabled ) {
					continue;
				}

				highlight = !highlight;

				EditorGUILayout.BeginHorizontal( ( highlight ) ? highlightStyle : noStyle );

				//# This must be a directory...
				if ( i < pathArray.Length - 1 ) {
					pathNodes.Add( pathArray[ i ] );

					if ( !foldoutValues.ContainsKey( pathArray[ i ] ) ) {
						foldoutValues.Add( pathArray[ i ], true );
					}
					
					iterationIsDir = true;
				} else {
					iterationIsDir = false;
				}

				if ( !checkboxValues.ContainsKey( change.Path ) ) {
					checkboxValues.Add( change.Path, true );
				}

				GUILayout.Space( i * spacing );

				if ( !iterationIsDir ) {
					checkboxValues[ change.Path ] = GUILayout.Toggle( checkboxValues[ change.Path ], pathArray[ i ] );
					GUILayout.Label( "[" + change.Status + "]", statusStyle );

					if ( GUILayout.Button( "Diff", GUILayout.Width( 50 ) ) ) {
						Diff.init( change.Patch );
					}

				}
				else {

					for ( int j = 0; j <= i; j++ ) {
						try {
							if ( !foldoutValues[ pathArray[ j ] ] ) {
								GUI.enabled = false;
								break;
							}
							else {
								GUI.enabled = true;
							}
						}
						catch {}
					}

					foldoutValues[ pathArray[ i ] ] = EditorGUILayout.Foldout( foldoutValues[ pathArray[ i ] ], pathArray[ i ] );

					//GUI.enabled = foldoutValues[ pathArray[ i ] ];
				}

				EditorGUILayout.EndHorizontal();
			}
		}

		/*private void recurseToAssetFolder( string change, ref bool highlight ) {
			int spacing = 20;
			bool iterationIsDir = false;
			string[] pathArray = change.Split( "\\".ToCharArray() );

			for ( int i = 0; i < pathArray.Length; i++ ) {

				if ( pathNodes.Contains( pathArray[ i ] ) ) {
					continue;
				}

				highlight = !highlight;

				EditorGUILayout.BeginHorizontal( ( highlight ) ? highlightStyle : noStyle );

				//# This must be a directory...
				if ( i < pathArray.Length - 1 ) {
					pathNodes.Add( pathArray[ i ] );

					if ( !foldoutValues.ContainsKey( pathArray[ i ] ) ) {
						foldoutValues.Add( pathArray[ i ], true );
					}

					iterationIsDir = true;
				} else {
					iterationIsDir = false;
				}

				if ( !checkboxValues.ContainsKey( change ) ) {
					checkboxValues.Add( change, true );
				}

				GUILayout.Space( i * spacing );

				if ( !iterationIsDir ) {
					checkboxValues[ change ] = GUILayout.Toggle( checkboxValues[ change ], pathArray[ i ] );
					GUILayout.Label( "[Unversioned]", statusStyle );

					if ( GUILayout.Button( "Diff", GUILayout.Width( 50 ) ) ) {
						Diff.init( change );
					}

				} else {
					foldoutValues[ pathArray[ i ] ] = EditorGUILayout.Foldout( foldoutValues[ pathArray[ i ] ], pathArray[ i ] );
					
					GUI.enabled = foldoutValues[ pathArray[ i ] ];
				}

				EditorGUILayout.EndHorizontal();
			}
		}*/

		public static Texture2D getGenericTexture( int width, int height, Color col ) {
			Color[] pix = new Color[ width * height ];

			for ( int i = 0; i < pix.Length; i++ ) {
				pix[ i ] = col;
			}

			Texture2D result = new Texture2D( width, height );

			result.SetPixels( pix );
			result.Apply();

			result.hideFlags = HideFlags.HideAndDontSave;
			result.hideFlags ^= HideFlags.NotEditable;

			return result;
		}
	}

}

