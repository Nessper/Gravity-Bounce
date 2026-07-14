# Audio, localisation et dialogues

> **Périmètre** : musique, SFX, ducking/pause, langues, packs de textes, équipage et narration de niveau.  
> **Statut** : confirmé statiquement ; mix et synchronisation audio non écoutés.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `AudioManager.cs`, `LevelMusicDirector.cs`, `LocalizationManager.cs`, scripts `Narration`, JSON `Resources/Localization`, ScriptableObjects `Data/Narration/Crew`.

## Audio global

`AudioManager`, installé au Boot, fournit musique et effets globaux par identifiants. Il gère transitions/crossfades de musique, ducking pendant certaines présentations et comportement de pause. Les boutons, impacts, dialogues et séquences appellent des émetteurs spécialisés ou le manager.

`LevelMusicDirector` choisit pour un niveau une paire briefing/gameplay parmi les clips disponibles dans `Resources`, avec sélection aléatoire observée, puis coordonne les transitions avec le flux de mission.

## Voix et texte de dialogue

Les ScriptableObjects d’équipage définissent les personnages (Cal Rydell, Leigh, Mike, Operator, Rye, Seller) et `CrewDatabase` les indexe. Les contrôleurs de narration choisissent les entrées de dialogue, affichent le locuteur/portrait et pilotent l’effet de frappe ainsi que les SFX associés.

Les packs de dialogues acceptent des variantes pondérées. Le résultat précis d’une sélection est donc non déterministe lorsque plusieurs variantes sont admissibles.

## Localisation

Les domaines `ui`, `ships`, `modules` et `dialogs` possèdent chacun des fichiers `en.json` et `fr.json`. `LocalizationManager` charge les packs par langue et domaine, résout les clés et fournit des fallbacks lorsque nécessaire.

Dans `Boot`, la langue observée est fixée à `fr`. Aucun écran de changement persistant de langue n’a été établi. Le contenu reste hybride : certains titres de niveau, objectifs et crédits sont directement en anglais, des textes de scan sont en français et plusieurs fallbacks sont codés en dur.

## Élément historique

Une génération de lecteur musical de titre coexiste avec la musique globale du Boot. Sa classification et les références retrouvées sont centralisées dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).
