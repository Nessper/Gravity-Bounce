# Guide de travail IA sur 404

> **Usage** : méthode à suivre par un agent IA avant, pendant et après une future modification du projet.  
> **Statut** : guide opératoire fondé sur l’architecture observée ; il ne donne aucune autorisation de modifier le projet.  
> **Dernière vérification** : 2026-07-23.
> **Contexte associé** : [AI_CONTEXT_404.md](AI_CONTEXT_404.md) et [AI_SYSTEMS_REFERENCE_404.md](AI_SYSTEMS_REFERENCE_404.md).

## 1. Ordre de lecture recommandé

L’ordre dépend de la disponibilité du dépôt, mais commence toujours par le contexte global avant les détails système.

### Sans accès au dépôt

1. Lire [AI_CONTEXT_404.md](AI_CONTEXT_404.md) pour le flux, les sources de vérité, l’état actuel et les incertitudes.
2. Lire dans [AI_SYSTEMS_REFERENCE_404.md](AI_SYSTEMS_REFERENCE_404.md) les domaines concernés et leurs dépendances entrantes/sortantes.
3. Utiliser ce guide pour préparer questions, périmètre, risques et validations.
4. Ne pas inventer un comportement absent ; marquer ce qui exige l’accès aux sources ou Play Mode.

### Avec accès au dépôt

1. Lire [`docs/README.md`](../README.md) et les pages canoniques indiquées par la carte ci-dessous.
2. Lire [`incertitudes-contradictions-et-travaux-en-cours.md`](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md) et [`systemes-actifs-legacy-et-hybrides.md`](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).
3. Vérifier l’état Git courant avant d’ouvrir ou modifier un asset.
4. Inspecter les sources métier, puis leurs références dans scènes/prefabs/ScriptableObjects/JSON.
5. Ne lancer Unity ou un build que si la tâche l’autorise et si les mutations d’import sont acceptables.

## 2. Choisir les documents à consulter

| Question | Point d’entrée documentaire |
|---|---|
| Quel est le flux général ? | [`architecture-globale.md`](architecture-globale.md), [`flux-runtime.md`](flux-runtime.md) |
| Qui possède une donnée ? | [`donnees-schemas-et-sources-de-verite.md`](../02-donnees-et-unity/donnees-schemas-et-sources-de-verite.md) |
| Quelle valeur est active ? | [`equilibrage-et-configuration-active.md`](../02-donnees-et-unity/equilibrage-et-configuration-active.md) |
| Quelle scène/prefab/SO câble le système ? | pages de [`docs/02-donnees-et-unity`](../02-donnees-et-unity/) |
| Quelle classe/ID/chemin chercher ? | pages de [`docs/03-reference`](../03-reference/) |
| Est-ce actif, legacy ou hybride ? | [`systemes-actifs-legacy-et-hybrides.md`](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md) |
| Le comportement est-il confirmé ? | [`incertitudes-contradictions-et-travaux-en-cours.md`](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md) |
| Comment maintenir la documentation ? | [`maintenance-documentaire.md`](../05-maintenance/maintenance-documentaire.md) |

Une page d’architecture explique les relations ; la page système porte le comportement ; les pages données/Unity portent la propriété et le câblage ; le registre d’incertitudes est l’unique autorité documentaire sur les contradictions ouvertes.

## 3. Méthode obligatoire avant toute proposition de modification

Les huit étapes suivantes s’appliquent dans l’ordre ; une étape peut conclure qu’une question humaine ou une preuve runtime est nécessaire.

### Étape 1 — Reformuler le symptôme ou l’objectif

- Distinguer le résultat attendu de la solution suggérée.
- Identifier le parcours concerné : Boot, écran, hub, mission, flush, fin, reprise ou debug.
- Noter plateforme, scène, niveau, vaisseau, modules, état de sauvegarde et conditions de reproduction disponibles.
- Ne pas transformer une intention supposée en exigence.

### Étape 2 — Vérifier la documentation

- Ouvrir la page système canonique et les pages de données/câblage associées.
- Consulter systématiquement le registre legacy/hybride et le registre d’incertitudes.
- Vérifier la date et le statut de chaque affirmation utilisée.
- Si la documentation et les sources divergent, décrire la divergence avant de conclure ; le code/asset présent reste l’autorité sur l’état logiciel.

### Étape 3 — Tracer les systèmes impactés

Construire une chaîne « entrée → propriétaire → transformation → consommateurs → persistance ». Exemple générique :

```text
JSON/SO/scène → service ou manager → état runtime → événements/binders
             → UI/FX/audio → snapshot/sauvegarde/analytics/navigation
```

Inclure les dépendances indirectes. Une modification du score peut par exemple atteindre progression, objectifs, combos, cérémonie, analytics et meilleur score, même si le symptôme est visuel.

### Étape 4 — Vérifier toutes les représentations Unity concernées

- **Scripts** : producteurs, consommateurs, interfaces, événements, chemins debug.
- **Scènes** : instances, activation, références, valeurs surchargées et ordre de chargement.
- **Prefabs** : source du prefab, overrides d’instance, variantes et anciens prefabs coexistants.
- **ScriptableObjects** : valeurs sérialisées, mutation runtime, chargement et références.
- **JSON/Resources** : schéma attendu, IDs, chemins logiques, fallbacks et données historiques.
- **ProjectSettings/Packages** : seulement si input, rendu, build, compilation ou plateforme sont concernés.

Une lecture du script seul ne prouve pas que la classe est instanciée ni que ses champs sont renseignés.

### Étape 5 — Classer les affirmations

Utiliser explicitement :

- **Fait confirmé** : déclaration et branchement retrouvés.
- **Observation statique** : référence/câblage visible sans exécution.
- **Hypothèse de travail** : interprétation nécessaire, annoncée comme telle.
- **Incertain** : contradiction, ordre runtime ou intention manquante.

Une hypothèse ne devient pas un fait parce qu’elle explique bien le symptôme.

### Étape 6 — Rechercher la cause racine

- Suivre la donnée depuis sa source de vérité jusqu’au symptôme.
- Vérifier si le comportement vient du chemin actuel ou d’une couche legacy encore câblée.
- Distinguer calcul métier, persistance, orchestration et présentation.
- Vérifier les chemins normal, reprise, debug et lancement direct lorsqu’ils partagent le système.
- Ne pas attribuer à l’UI une valeur produite par le score, ni au score un problème de présentation sans preuve.

### Étape 7 — Évaluer les effets de bord

Pour chaque consommateur, examiner :

- nouvelle run, run existante et sauvegarde ancienne ;
- réussite, défaite, abandon, interruption et reprise ;
- hub, mission, cérémonie et sortie ;
- vaisseaux, niveaux, tiers et modules différents ;
- clavier/souris/tactile et plateforme ;
- UI, audio, FX, analytics et outils debug ;
- valeurs sérialisées, événements statiques et objets persistants.

### Étape 8 — Formuler la modification minimale

Dans une tâche future autorisant les changements, proposer l’intervention la plus petite qui agit sur la source de vérité correcte, conserve les contrats publics utiles et limite les assets touchés. Indiquer séparément : fichiers prévus, comportement attendu, migrations éventuelles, validations et incertitudes restantes. Une refonte non demandée ne découle pas automatiquement d’une anomalie observée.

## 4. Règles de prudence propres à Unity

Ces règles complètent l’analyse C# en couvrant les sources d’état et de câblage propres à Unity.

### Références sérialisées

- Un champ C# non nul par défaut peut être nul dans une scène ; l’inverse peut être injecté au runtime.
- Rechercher les références par GUID/script et inspecter les valeurs/overrides des instances.
- Un GameObject actif dans le YAML ne garantit pas que son contrôleur reçoit le flux fonctionnel.

### Scènes et prefabs

- Identifier la source du prefab et les overrides avant toute édition.
- Vérifier les scènes du build et les scènes hors build séparément.
- Ne pas confondre les anciennes UI de fin encore présentes avec le chemin cérémonie/`EndResult` actuel.
- Ouvrir Unity peut réimporter ou réécrire des assets ; ne le faire que dans un périmètre autorisé.

### GUID et fileID

- Préserver les fichiers `.meta` et leurs GUID lors d’un déplacement/renommage autorisé.
- Ne pas fabriquer manuellement un `fileID` ou une référence YAML sans preuve de sa cible.
- Une recherche `m_Script: {fileID: 0}` négative ne garantit pas à elle seule un projet importable.

### Ordre d’initialisation et objets persistants

- `Boot` installe le root global ; `Main` normale suppose ces services présents.
- Examiner `Awake`, `OnEnable`, `Start`, `DontDestroyOnLoad`, abonnements et désabonnements.
- Comparer parcours Boot, `DebugLauncher` et `MainStandaloneInstaller`.
- L’ordre entre `SaveManager` et `RunRecoveryOnBoot` reste une incertitude documentée.

### Événements statiques

- Rechercher abonnements multiples, désabonnements et resets aux frontières de scène/run.
- `ChainRuntimeState` et `TimingRuntimeState` sont statiques, mais `FlushResolutionEngine.ResetRuntimeState()` borne maintenant leur durée de vie et efface aussi l’UI.
- Vérifier que tout nouveau chemin de tutoriel/retry/arrêt/fin utilise cette API commune et que la fin normale ne l’appelle qu’après le flush final et ses impacts visuels.

### ScriptableObjects

- Distinguer asset de configuration et conteneur mutable runtime.
- `RunSessionState.asset` est synchronisé avec la sauvegarde ; sa valeur sérialisée n’est pas nécessairement l’état de jeu effectif.
- Vérifier si une valeur provient d’un SO, d’une scène, d’un JSON ou d’une constante avant de la modifier.

### PlayerPrefs et versions

- La sauvegarde JSON utilise `GameSave_v1` et la version `VS_GAME_VERSION`.
- Le changement de version déclenche `PlayerPrefs.DeleteAll()` dans le chemin observé.
- Tester conceptuellement nouvelle sauvegarde, sauvegarde existante, données invalides, niveau en cours et fin pending.
- Le stockage est éditable : le token de fin protège l’idempotence, pas contre un utilisateur hostile.

### Modifications locales non commitées

- Lire `git status` avant et après toute action.
- Les changements existants appartiennent à leur auteur ; ne pas les écraser, nettoyer ou reformater sans autorisation.
- Dans l’instantané 2026-07-14, scènes, polices, combo, module et nouvelle UI de score étaient déjà modifiés/non suivis ; consulter [AI_CONTEXT_404.md](AI_CONTEXT_404.md) pour le détail historique, puis vérifier l’état courant.
- Si la tâche chevauche une zone déjà modifiée, isoler précisément les lignes/assets et demander une intention lorsqu’elle ne peut pas être déduite.

## 5. Règles de documentation

- La documentation du dépôt reste la référence lorsque le dépôt est accessible ; ces trois synthèses servent de contexte portable.
- Une information détaillée ne doit vivre qu’à un emplacement canonique. Ailleurs, résumer et lier.
- Après un changement autorisé, réviser la page système propriétaire, puis les index seulement si les flux/IDs/liens changent.
- Conserver **Périmètre**, **Statut**, **Date de vérification** et **Principaux appuis** dans les documents canoniques.
- Laisser les incertitudes explicitement marquées tant qu’une preuve code/donnée/runtime ne les résout pas.
- Ne pas supprimer une mention legacy parce qu’un chemin semble inutilisé ; vérifier ses références et son statut réel.
- Ne pas recopier de grandes tables JSON dans plusieurs documents ; pointer vers la source et la page d’équilibrage.

## 6. Carte de lecture par type de modification

Chaque entrée associe un domaine futur aux documents et représentations qu’il faut inspecter avant de proposer un changement.

### Modules

Lire :

- [`economie-boutique-et-modules.md`](../01-systemes/economie-boutique-et-modules.md)
- [`paddle-bacs-et-flush.md`](../01-systemes/paddle-bacs-et-flush.md)
- [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md)
- [`donnees-schemas-et-sources-de-verite.md`](../02-donnees-et-unity/donnees-schemas-et-sources-de-verite.md)
- [`equilibrage-et-configuration-active.md`](../02-donnees-et-unity/equilibrage-et-configuration-active.md)

Vérifier : `ModuleCatalog.json`, localisation modules, services `RunModule*`, équipement/slots, boutique, points d’application et sauvegarde. Les sélections Shop/inventaire/briefing et Ship Systems sont réinitialisées aux transitions ; un module non équipé peut être installé par slot surligné ou double-clic ; l’achat déclenche le pulse temporaire du bouton Tuning. Pour les drones, inspecter d’abord `DroneRuntimeControllerBase` et `ModuleRuntimeStats` : K0 doit rester transversal et ne connaître aucune classe concrète. Pour K1, vérifier `K1AntiBlackDroneController`, les deux `BinTrigger`, la priorité de la famille A et l’arbitrage `collected` avec les snapshots. Pour K2, vérifier `K2DroneInterceptorController`, `DroneInterceptionZone`, l’exclusion propriétaire dans `BallState`, les gardes de `BinTrigger`/`VoidTrigger`, la position de réentrée du `BallSpawner` et la libération forcée du nettoyage final.

### Score, progression ou combos

Lire :

- [`score-objectifs-et-combos.md`](../01-systemes/score-objectifs-et-combos.md)
- [`paddle-bacs-et-flush.md`](../01-systemes/paddle-bacs-et-flush.md)
- [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md)
- registre des [`incertitudes`](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md)

Vérifier : `FlushResolutionEngine`, `ScoreManager`, états/règles de combo, objectifs, snapshot, HUD, cérémonie, analytics et meilleur score. Le chemin UI de score V2 est câblé et validé en Play Mode ; l’ancien chemin `ScoreBinder`/`ScoreUI` a été retiré.

### Sauvegarde, progression permanente ou reprise

Lire :

- [`sauvegarde-progression-et-integrite.md`](../01-systemes/sauvegarde-progression-et-integrite.md)
- [`run-nodes-vaisseaux-et-ressources.md`](../01-systemes/run-nodes-vaisseaux-et-ressources.md)
- [`invariants-cycles-de-vie-et-persistance.md`](../03-reference/invariants-cycles-de-vie-et-persistance.md)
- [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md)

Vérifier : modèle `GameSaveData`, defaults/validation, clés/version, synchronisation `RunSessionState`, abandon, pending snapshot/token et chemins de debug.

### Spawn, balles, physique ou obstacles

Lire :

- [`niveaux-spawn-balles-et-physique.md`](../01-systemes/niveaux-spawn-balles-et-physique.md)
- [`paddle-bacs-et-flush.md`](../01-systemes/paddle-bacs-et-flush.md)
- [`equilibrage-et-configuration-active.md`](../02-donnees-et-unity/equilibrage-et-configuration-active.md)
- [`prefabs-scriptableobjects-et-assets-runtime.md`](../02-donnees-et-unity/prefabs-scriptableobjects-et-assets-runtime.md)

Vérifier : JSON du niveau, schéma de mix, `BallSpawner`, pool, quatre définitions, prefab/colliders/triggers, cleanup final, tutoriel et tuning. Le SCAN est calculé par `ScanT1AnalysisBuilder`, tandis que le log `[SpawnPlan]` décrit les files effectivement construites par le spawner.

### Fin de niveau, récompenses ou cérémonie

Lire :

- [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md)
- [`score-objectifs-et-combos.md`](../01-systemes/score-objectifs-et-combos.md)
- [`sauvegarde-progression-et-integrite.md`](../01-systemes/sauvegarde-progression-et-integrite.md)
- [`systemes-actifs-legacy-et-hybrides.md`](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md)

Vérifier : tous les déclencheurs, évacuation/cleanup, évaluateurs, bonus de modules, outcome/snapshot/token, deux overlays actuels, commit, reprise, analytics et transition.

### Scènes, services globaux ou flow

Lire :

- [`architecture-globale.md`](architecture-globale.md)
- [`flux-runtime.md`](flux-runtime.md)
- [`scenes-et-cablage.md`](../02-donnees-et-unity/scenes-et-cablage.md)
- [`index-classes-dependances-et-evenements.md`](../03-reference/index-classes-dependances-et-evenements.md)
- [`debug-et-outils-alpha.md`](../01-systemes/debug-et-outils-alpha.md)

Vérifier : build settings, root Boot, `DontDestroyOnLoad`, dépendances de `Main`, chemin standalone/debug, références sérialisées et transitions actuelle/legacy.

### UI, audio, localisation ou FX

Lire la page système correspondante dans [`docs/01-systemes`](../01-systemes/), puis [`scenes-et-cablage.md`](../02-donnees-et-unity/scenes-et-cablage.md) et [`prefabs-scriptableobjects-et-assets-runtime.md`](../02-donnees-et-unity/prefabs-scriptableobjects-et-assets-runtime.md). Vérifier que la vue reste consommatrice et ne devient pas une seconde source de vérité métier.

## 7. Zones actuellement dangereuses ou hybrides

Ces zones exigent davantage de vérifications ; elles ne sont pas déclarées défectueuses :

1. valeur finale `ptsJustInTime` non sérialisée ;
2. limite finale du `RunPlan` ;
3. récupération distribuée entre `SaveManager` et `RunRecoveryOnBoot` ;
4. déblocage/visibilité des vaisseaux et sixième layout Aether Runner ;
5. ancien/nouveau input et comportement tactile ;
6. schéma de niveau historique, surtout DBG-L1 ;
9. trois générations d’UI de fin et deux générations de transition ;
10. nouvelle UI de score avec fichiers locaux et références nulles observées ;
11. identité 404/VoidScrappers et clés/version de sauvegarde historiques ;
12. localisation mixte et lecteur musical de titre historique ;
13. testeurs/loggers actifs dans `Main` ;
14. analytics de début de run conditionné par l’index 0.

Le détail factuel de chaque point reste dans [`incertitudes-contradictions-et-travaux-en-cours.md`](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## 8. Questions à poser lorsque l’intention manque

Ne poser que les questions qui changent réellement le résultat. Exemples :

- Le comportement demandé concerne-t-il le chemin normal, debug, standalone ou tous les chemins ?
- Le changement doit-il s’appliquer aux runs/sauvegardes existants ou seulement aux nouveaux ?
- Quelle ressource est visée : coque, vie de contrat, argent, score ou progression ?
- Une balle noire doit-elle être traitée avant ou après les transformations de modules dans le cas demandé ?
- L’intention concerne-t-elle le calcul métier ou seulement sa présentation ?
- Quelle génération d’écran/transition est visée lorsque plusieurs coexistent ?
- Les fichiers locaux non commités dans la zone appartiennent-ils à un travail à préserver et compléter ?
- Le déblocage/masquage des vaisseaux doit-il suivre les champs du catalogue ou le comportement actuel de sélection ?
- Le comportement mobile/tactile doit-il être inclus et sur quelle plateforme/appareil ?
- Le « retry » signifie-t-il recharger la mission, reprendre un snapshot ou démarrer un nouveau nœud ?
- Un terme absent des sources, comme « scorie » ou « token » économique, possède-t-il une définition produit externe ?
- Quelle preuve runtime est acceptable pour résoudre une incertitude documentée ?

## 9. Checklist avant modification

- [ ] La tâche autorise explicitement les fichiers et types d’assets à modifier.
- [ ] Le symptôme/objectif et les critères d’acceptation sont reformulés sans supposer la solution.
- [ ] L’état Git courant est lu et les changements préexistants sont identifiés.
- [ ] Les pages canoniques du système, des données, du câblage, du legacy et des incertitudes sont lues.
- [ ] La source de vérité de chaque donnée est identifiée.
- [ ] Les producteurs, consommateurs, événements et persistances sont cartographiés.
- [ ] Scripts, scènes, prefabs, SO et JSON concernés ont été inspectés selon le besoin.
- [ ] Les chemins normal, fin, abandon/reprise et debug pertinents sont considérés.
- [ ] Faits, observations statiques, hypothèses et inconnues sont séparés.
- [ ] Les effets de bord UI/audio/FX/analytics/plateforme sont évalués.
- [ ] Les sauvegardes existantes, IDs, GUID/fileID et version sont pris en compte.
- [ ] La modification minimale et les validations proportionnées sont définies.
- [ ] Toute intention introuvable a fait l’objet d’une question au lieu d’une invention.

## 10. Checklist après modification

- [ ] `git diff`/état Git ne contient que les fichiers autorisés et préserve les changements préexistants.
- [ ] Les références sérialisées, `.meta`, GUID et overrides n’ont pas été altérés involontairement.
- [ ] Le comportement principal et les cas limites pertinents ont été vérifiés au niveau autorisé.
- [ ] Nouvelle run, run existante, réussite, défaite, abandon et reprise ont été considérés si concernés.
- [ ] Les chemins Boot, debug et standalone ont été vérifiés si la dépendance globale a changé.
- [ ] Les abonnements/désabonnements et resets statiques ont été vérifiés si des événements ont changé.
- [ ] Les données JSON/SO et leurs consommateurs utilisent toujours les mêmes IDs/schémas.
- [ ] Le snapshot/commit reste cohérent si score, récompenses ou fin ont changé.
- [ ] UI, audio, FX et analytics reflètent le résultat sans devenir sources de vérité concurrentes.
- [ ] La page documentaire canonique concernée a été mise à jour sans dupliquer le détail ailleurs.
- [ ] Les incertitudes résolues ou nouvelles sont mises à jour explicitement.
- [ ] Les limites de validation et les vérifications humaines restantes sont signalées.

## Limite du guide

Ce guide indique comment raisonner ; il ne remplace ni l’autorisation de modifier, ni l’inspection du dépôt, ni une validation humaine d’intention. Les faits d’architecture portables se trouvent dans [AI_CONTEXT_404.md](AI_CONTEXT_404.md), et les dépendances par domaine dans [AI_SYSTEMS_REFERENCE_404.md](AI_SYSTEMS_REFERENCE_404.md).
