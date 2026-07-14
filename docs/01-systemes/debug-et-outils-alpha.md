# Debug et outils alpha

> **Périmètre** : scène de lancement debug, injection de contexte, installateurs standalone, testeurs runtime et outils Editor.  
> **Statut** : inventaire confirmé ; effets non déclenchés.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `DebugLauncher.unity`, scripts sous `Assets/Project/Scripts/Debug`, `MainStandaloneInstaller.cs`, scripts `Gameplay/Combos/Debug`, objets sérialisés de `Main.unity`.

## Chemins de lancement

`DebugLauncher` est une scène incluse au build qui permet de choisir/injecter un contexte avant de charger `Main`. `MainDebugStarterV3` et ses outils Editor configurent monde, niveau, vaisseau et autres paramètres de test. `MainStandaloneInstaller` installe les dépendances minimales lorsque `Main` est lancée sans le parcours Boot normal.

Ces chemins peuvent créer ou modifier l’état local de jeu. Ils ne constituent donc pas une frontière anti-triche.

## Outils runtime

Le projet contient notamment : raccourci de rapport de bug, activation de logo debug, bouton de debug Main, journalisation de raycasts UI, détection d’écriture de scale UI, scan de scripts manquants et test de barre de progression.

Dans la scène `Main` observée, `ComboSessionTester`, `ChainRuntimeDebugLogger`, `FinalComboTester`, `FlushTest` et `MissingScriptScanner` sont attachés à des GameObjects actifs. Leur présence active est un fait de sérialisation ; leur exécution effective dépend de leurs propres drapeaux et callbacks.

## Outils Editor

`VoidScrappersDebugMenu` et `MainDebugStarterV3Editor` ajoutent des commandes/inspecteurs dans l’éditeur. Ils sont exclus du player par leur emplacement/compilation Editor. Aucun test automatisé de gameplay n’a été retrouvé malgré la présence du package Unity Test Framework.

## Données debug

Le catalogue de niveaux référence `DBG-L1` et le catalogue de vaisseaux contient `DEBUG_SHIP`. Le niveau debug utilise en partie un ancien schéma de mélange de balles ; son résultat exact est signalé dans [Niveaux, spawn, balles et physique](niveaux-spawn-balles-et-physique.md).
