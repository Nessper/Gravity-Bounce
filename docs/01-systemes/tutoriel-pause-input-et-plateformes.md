# Tutoriel, pause, input et plateformes

> **Périmètre** : tutoriel embarqué, mise en pause, voies de contrôle souris/tactile/clavier et adaptations de plateforme.  
> **Statut** : confirmé statiquement ; interactions multi-plateformes non exécutées.  
> **Date de vérification** : 2026-07-23.
> **Principaux appuis** : scripts `Gameplay/Tutorial`, `LevelPauseFlowHandler.cs`, `PlayerController.cs`, scripts `Input`, `PlatformTuning.cs`, prefab joueur et réglages Player.

## Tutoriel

Le tutoriel se déclenche sur W1-L1 tant que le drapeau permanent de complétion est faux. Il déroule quatre étapes guidées, contrôle des spawns et interactions ciblées, et sauvegarde la complétion dans le profil.

La séquence capture puis restaure une partie de l’état de jeu afin de ne pas conserver ses conditions artificielles. Les états statiques et l’UI de chaînes sont explicitement réinitialisés via la même API que les autres frontières de mission. Le contenu précis des quatre étapes est porté par les contrôleurs de tutoriel et leurs références sérialisées dans `Main`.

## Pause

`LevelPauseFlowHandler` coordonne le niveau et `PauseOverlayController`. La pause suspend le temps de jeu et informe l’audio afin d’appliquer son comportement dédié. Les options de reprise et sortie sont gérées par l’overlay ; le traitement d’une sortie/abandon rejoint les marqueurs de sauvegarde décrits dans [Sauvegarde](sauvegarde-progression-et-integrite.md).

`LevelControlsController` conserve séparément la demande métier d’activation des contrôles et le verrou temporaire de pause. Pendant la pause, il désactive ensemble le paddle, les commandes de fermeture des bacs et la racine mobile. À la reprise, il ne réactive que ce que le flux de gameplay demandait déjà. Le retry remet également à zéro les chaînes avant de relancer la mission.

## Voies d’input

Le projet active « Both » dans le réglage Unity : ancien Input Manager et nouveau Input System peuvent coexister. Le gameplay retrouvé utilise principalement `UnityEngine.Input`.

- `PlayerController` lit une logique de delta souris dans sa configuration active.
- `PlayerInputMouse` et `PlayerInputTouch` peuvent produire une cible monde externe.
- `CloseBinInputKeyboard` et `CloseBinInputTouch` fournissent les deux voies explicites de fermeture des bacs.
- `CursorController` adapte l’état du curseur à la voie de contrôle utilisée.
- les boutons UI utilisent les événements Unity UI ; clavier/échappement et raccourcis debug sont lus directement.
- un asset d’actions du nouveau Input System est assigné dans les build settings, mais son usage par le gameplay principal n’a pas été retrouvé ; son contenu ressemble au modèle générique Unity.

La relation exacte entre le mode delta du prefab et les cibles monde externes, ainsi que le comportement tactile réel, sont centralisés comme **incertitude** dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Plateformes

`PlatformTuning`, `MobileOnlyUI` et les branches de compilation/plateforme adaptent affichage et contrôle. `AspectRatioKeeper` maintient le cadre attendu. Les réglages Player ciblent notamment Android, mais aucun test de build ou d’appareil n’a été réalisé pour cette documentation.
