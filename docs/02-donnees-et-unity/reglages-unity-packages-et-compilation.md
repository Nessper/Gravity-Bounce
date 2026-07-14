# Réglages Unity, packages et compilation

> **Périmètre** : version Unity, pipeline, packages, entrées, structure de compilation et identité Player.  
> **Statut** : confirmé par fichiers de projet ; compilation/build non lancés.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `ProjectVersion.txt`, `ProjectSettings.asset`, `EditorBuildSettings.asset`, `Packages/manifest.json`, arborescence des scripts.

## Environnement

- Unity : `6000.2.6f2`.
- Pipeline : Universal Render Pipeline `17.2.0`.
- UI : uGUI `2.0.0` et TextMeshPro via l’écosystème Unity.
- Input System : `1.14.2`, avec `activeInputHandler: 2` (« Both »).
- Test Framework : `1.6.0`, sans suite de tests projet retrouvée.
- Timeline, Recorder, Visual Scripting, AI Navigation et modules Unity standards sont déclarés.

## Compilation

Aucun fichier `.asmdef` propre au projet n’a été retrouvé. Les scripts runtime compilent donc principalement dans `Assembly-CSharp`; les scripts sous dossiers `Editor` dans l’assembly Editor implicite. Des namespaces `VoidScrappers` coexistent avec des classes sans namespace et l’identité 404.

## Identité Player

Le produit est nommé `404 - A Space Arcade Roguelite` et la société `Team Leeward`. Des identifiants historiques `VS_*`, namespaces VoidScrappers et un ancien bundle Android coexistent avec cette identité ; leur présence est décrite dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).

## Entrées et build

Un asset Input System est associé aux build settings, mais le gameplay utilise surtout l’API d’input historique. Les sept scènes actives sont inventoriées dans [Scènes et câblage](scenes-et-cablage.md). Aucun build, import propre, test de compilation ou Play Mode n’a été effectué afin de respecter le périmètre documentaire sans mutation d’assets.
