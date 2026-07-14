# Référence IA condensée des systèmes de 404

> **Usage** : fiches autonomes pour raisonner sur les systèmes sans parcourir les 258 scripts de jeu.
> **Statut** : synthèse statique de l’état observé, pas une spécification d’intention.  
> **Dernière vérification** : 2026-07-14.  
> **Compléments** : [contexte global](AI_CONTEXT_404.md) et [guide de travail](AI_WORKING_GUIDE_404.md).

## Comment lire une fiche

Chaque domaine indique rôle, état, fonctionnement/cycle, données, persistance, points structurants, dépendances entrantes/sortantes, surface publique, cas limites, validations et document canonique. **Actif** signifie relié au chemin principal par analyse statique, pas validé en Play Mode. **Incertain** ne doit jamais être transformé en fait.

## 1. Démarrage et services persistants

- **Rôle — état** : installer le contexte global avant les autres scènes ; **actif**, avec chemins alternatifs debug/standalone.
- **Fonctionnement — cycle** : `Boot` crée un root unique conservé entre scènes ; les doublons doivent être évités par les gardes du bootstrap.
- **Données — persistance** : références sérialisées aux configurations/services ; le root persiste en mémoire, tandis que `SaveManager` porte la persistance disque.
- **Classes/assets** : `Bootstrapper`, `BootRoot`, `RunSessionBootstrapper`, prefab/configuration de run, `Boot.unity`.
- **Entrantes → sortantes** : scène d’index 0 et références Boot → sauvegarde, flow, audio, localisation, analytics, modules, tuning, récupération.
- **Surface publique** : instances globales/services, initialisation et synchronisation de `RunSessionState`.
- **Cas limites/validation** : ordre `Awake`/`Start`, doubles roots et exécution directe de `Main` non vérifiés en Play Mode.
- **Canonique** : [`architecture-globale.md`](architecture-globale.md), [`scenes-et-cablage.md`](../02-donnees-et-unity/scenes-et-cablage.md).

## 2. Navigation et scènes

- **Rôle — état** : charger les écrans, le hub, la mission et les crédits ; **actif**, transitions partiellement hybrides.
- **Fonctionnement — cycle** : `Boot → Title → ShipSelect → RunHub ↔ Main`, puis fin de run/crédits ; `DebugLauncher → Main` pour le test.
- **Données — persistance** : noms/index de scènes dans `EditorBuildSettings`; destination courante dérivée du run, qui persiste.
- **Classes/assets** : `GameFlowController`, `RunNavigator`, `MainExitTransitionController`; sept scènes de build.
- **Entrantes → sortantes** : commandes UI et nœud courant → `SceneManager`, transitions et initialisation de la scène suivante.
- **Surface publique** : méthodes de navigation du flow et du run ; actions de boutons UI.
- **Cas limites/validation** : `NextTransitionController` est une génération antérieure ; fin exacte du plan incertaine ; deux scènes auxiliaires sont hors build.
- **Canonique** : [`flux-runtime.md`](flux-runtime.md), [`scenes-et-cablage.md`](../02-donnees-et-unity/scenes-et-cablage.md).

## 3. Sauvegarde, migrations et reprise

- **Rôle — état** : conserver profil, run et transactions de fin ; **actif**, reprise répartie entre deux composants.
- **Fonctionnement — cycle** : `SaveManager` charge/sérialise `GameSaveData` en JSON dans `PlayerPrefs`; un changement de `VS_GAME_VERSION` appelle `PlayerPrefs.DeleteAll()`.
- **Données — persistance** : clé `GameSave_v1`; profil, argent, runState, offres, équipements, marqueurs de niveau et snapshot/token sont persistés.
- **Classes/assets** : `SaveManager`, `GameSaveData`, `RunRecoveryOnBoot`, `RunSessionState`.
- **Entrantes → sortantes** : mutations du run et fins de niveau → PlayerPrefs et événements de session ; Boot relit les marqueurs.
- **Surface publique** : chargement/sauvegarde, getters/setters de ressources, marqueurs d’entrée/abandon/fin.
- **Cas limites/validation** : ordre exact entre `SaveManager` et `RunRecoveryOnBoot` non exécuté ; stockage éditable, sans sécurité hostile.
- **Canonique** : [`sauvegarde-progression-et-integrite.md`](../01-systemes/sauvegarde-progression-et-integrite.md).

## 4. Progression permanente

- **Rôle — état** : conserver quelques résultats entre les runs ; **actif mais limité**, déblocage de vaisseaux **incertain**.
- **Fonctionnement — cycle** : meilleur score et tutoriel sont mis à jour aux moments prévus ; sélection/liste de vaisseaux vivent dans le profil.
- **Données — persistance** : `profileId`, vaisseau sélectionné, débloqués, meilleur score, `tutorialComplete`; argent/modules ne sont pas permanents.
- **Classes/assets** : `GameSaveData`, `SaveManager`, `ShipSelectController`, contrôleur de tutoriel.
- **Entrantes → sortantes** : fin de run/tutoriel/sélection → sauvegarde puis écrans futurs.
- **Surface publique** : sélection, complétion tutoriel, mise à jour du meilleur score.
- **Cas limites/validation** : l’écran ne filtre pas clairement `isHidden`/unlock et la sélection ajoute l’ID aux débloqués ; intention produit inconnue.
- **Canonique** : [`sauvegarde-progression-et-integrite.md`](../01-systemes/sauvegarde-progression-et-integrite.md), registre des [incertitudes](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## 5. Structure d’une run et nœuds

- **Rôle — état** : organiser boutiques, missions, boss et fin ; **actif**, frontière finale **incertaine**.
- **Fonctionnement — cycle** : `WorldCatalog` fournit des tokens, `RunPlanBuilder` crée les nœuds, `RunNavigator` avance et `RunHubController` route le nœud.
- **Données — persistance** : ID de run, monde, plan, index courant et état de nœud dans la sauvegarde/session.
- **Classes/assets** : `NewRunInitializer`, `RunPlan`, `RunNode`, `RunPlanBuilder`, `RunNavigator`, `RunSessionState`, `WorldCatalog.json`.
- **Entrantes → sortantes** : vaisseau/monde choisi → hub ou `Main`; résultat de mission → nœud suivant ou état d’échec.
- **Surface publique** : création de plan, lecture/avance du nœud, événements de changement de session.
- **Cas limites/validation** : convention `index == Count` contre bornage `Count - 1`; W2 possède des règles de shop mais aucun monde actif retrouvé.
- **Canonique** : [`run-nodes-vaisseaux-et-ressources.md`](../01-systemes/run-nodes-vaisseaux-et-ressources.md).

## 6. Vaisseaux

- **Rôle — état** : définir coque, durée, argent, slots et visuels ; **actif**, sélection/déblocage partiellement incertains.
- **Fonctionnement — cycle** : le catalogue est chargé avant sélection ; l’ID choisi initialise la run et configure `Main`/hub.
- **Données — persistance** : définition JSON statique ; ID choisi, slots et équipement persistés dans le run/profil selon le champ.
- **Classes/assets** : `ShipDefinition`, `ShipCatalogService`, `ShipSelectController`, `ShipRuntimeSetup`, `ShipCatalog.json`.
- **Entrantes → sortantes** : sélection utilisateur/catalogue → coque, temps, argent, layouts, visuels et slots de modules.
- **Surface publique** : résolution par ID et données de définition.
- **Cas limites/validation** : `AETHER_RUNNER` déclare 5 slots mais 6 layouts ; `DEBUG_SHIP` est caché ; filtrage réel à valider.
- **Canonique** : [`run-nodes-vaisseaux-et-ressources.md`](../01-systemes/run-nodes-vaisseaux-et-ressources.md), [`equilibrage-et-configuration-active.md`](../02-donnees-et-unity/equilibrage-et-configuration-active.md).

## 7. Coque et vies de contrat

- **Rôle — état** : représenter deux ressources de survie distinctes ; **actif**.
- **Fonctionnement — cycle** : coque initialisée depuis le vaisseau, modifiée par noires/modules/réparation ; une mission perdue peut consommer une vie de contrat.
- **Données — persistance** : coque courante/max/bonus et vies restantes persistent pendant la run ; trois vies au départ.
- **Classes/assets** : `HullSystem`, `HullGameOverWatcher`, `HullBinder`, `ContractLivesBinder`, `RunSessionState`.
- **Entrantes → sortantes** : flush et boutique → coque ; résultat de niveau → vies ; les deux → HUD, game over et fin de run.
- **Surface publique** : dégâts/soins/max, événements de coque/vies, consommation de vie.
- **Cas limites/validation** : ne jamais assimiler coque à vie de contrat ; ordre exact dégâts/fin anticipée à observer en Play Mode.
- **Canonique** : [`run-nodes-vaisseaux-et-ressources.md`](../01-systemes/run-nodes-vaisseaux-et-ressources.md).

## 8. Économie et monnaie

- **Rôle — état** : financer achats, rerolls et réparations ; **actif**, monnaie exclusivement de run.
- **Fonctionnement — cycle** : argent initial du vaisseau, gains par certains modules/récompenses, dépenses dans le hub, remise à zéro à la nouvelle run.
- **Données — persistance** : montant persiste pendant la run dans `GameSaveData`/`RunSessionState`; aucune monnaie permanente ni token économique retrouvé.
- **Classes/assets** : `SaveManager`, `RunSessionState`, `EconomyConfig_Default.asset` pour récompenses de médailles.
- **Entrantes → sortantes** : vaisseau/bonus → solde ; boutique/réparation/reroll → débit ; solde → UI.
- **Surface publique** : lecture, définition, ajout et débit du montant via services de run.
- **Cas limites/validation** : `EndLevelToken` est transactionnel, pas monétaire ; les valeurs du catalogue local peuvent être en cours de travail.
- **Canonique** : [`economie-boutique-et-modules.md`](../01-systemes/economie-boutique-et-modules.md).

## 9. Modules, inventaire et équipement

- **Rôle — état** : modifier briefing, durée, flush, coque, score et récompenses ; **actif**, catalogue local **en cours**.
- **Fonctionnement — cycle** : catalogue → offre → achat/possession → équipement dans un slot → agrégation runtime → effets au point d’application.
- **Données — persistance** : 24 définitions JSON, inventaire, IDs équipés, slots ouverts, charges/bonus pertinents dans le run.
- **Classes/assets** : `ModuleCatalogService`, `RunModuleEquipmentService`, `ModuleRuntimeStats`, services `RunModule*`, `ModuleCatalog.json`.
- **Entrantes → sortantes** : boutique et vaisseau → équipement ; équipement → briefing, timer, flush, coque, score, fin de niveau.
- **Surface publique** : résolution par ID, équiper/déséquiper, lecture des statistiques agrégées et événements de changement.
- **Cas limites/validation** : compatibilité slot/possession, ordre des transformations et données locales modifiées doivent être vérifiés avant changement.
- **Canonique** : [`economie-boutique-et-modules.md`](../01-systemes/economie-boutique-et-modules.md).

## 10. Boutique, reroll et réparation

- **Rôle — état** : proposer modules et services au hub ; **actif**.
- **Fonctionnement — cycle** : règles pondérées génèrent une offre persistée ; achat retire de l’argent ; reroll remplace l’offre ; repair restaure la coque.
- **Données — persistance** : règles `modules_shop_rules.json`; offres, nombre/état de rerolls, solde et coque persistent dans le run.
- **Classes/assets** : `RunHubModulesShopController`, `RunHubModulesBuyController`, `RunHubModulesRerollController`, `ShopRepairBayController`.
- **Entrantes → sortantes** : nœud/stage, catalogue, argent et coque → offres/actions → inventaire, solde, coque et UI.
- **Surface publique** : générer/acheter/reroll/réparer, rafraîchir les vues.
- **Cas limites/validation** : une offre rechargée ne doit pas reroll implicitement ; règles W2 présentes sans monde W2 actif.
- **Canonique** : [`economie-boutique-et-modules.md`](../01-systemes/economie-boutique-et-modules.md), [`equilibrage-et-configuration-active.md`](../02-donnees-et-unity/equilibrage-et-configuration-active.md).

## 11. Niveaux et configuration

- **Rôle — état** : définir mission, durée, cible, phases, objectifs, obstacles et narration ; **actif**, schéma **hybride**.
- **Fonctionnement — cycle** : `LevelCatalogService` charge l’index et le JSON du niveau ; `LevelBootstrapper` combine niveau, run, vaisseau et modules en `LevelContext`.
- **Données — persistance** : JSON W1-L1…W1-L6/DBG-L1 statiques ; résultat seulement persistant via snapshot/run.
- **Classes/assets** : `LevelData`, `LevelCatalogService`, `LevelContext`, `LevelBootstrapper`, `Resources/Levels`.
- **Entrantes → sortantes** : nœud courant/catalogue/modules → managers, briefing, timer, spawner, objectifs et obstacles.
- **Surface publique** : résolution de niveau par ID, contexte partagé.
- **Cas limites/validation** : anciens champs sans consommateur retrouvé ; DBG-L1 emploie `Mix.Type` au lieu de `BallId`.
- **Canonique** : [`niveaux-spawn-balles-et-physique.md`](../01-systemes/niveaux-spawn-balles-et-physique.md), [`donnees-schemas-et-sources-de-verite.md`](../02-donnees-et-unity/donnees-schemas-et-sources-de-verite.md).

## 12. Phases de spawn et pooling

- **Rôle — état** : produire les quotas de balles et recycler les instances ; **actif**.
- **Fonctionnement — cycle** : une phase est transformée en quota, types répartis selon le mix, positions tirées aléatoirement, instances sorties/replacées dans le pool.
- **Données — persistance** : phases/mix dans JSON ; quota courant, pool et positions sont temporaires.
- **Classes/assets** : `BallSpawner`, prefabs de balle, `BallDefinitionCatalog`; spawns forcés pour tutoriel/séquences.
- **Entrantes → sortantes** : `LevelContext`, timer et tutoriel → balles physiques → bacs/vide/cleanup.
- **Surface publique** : start/stop, phase suivante, spawn forcé, recyclage.
- **Cas limites/validation** : positions restent aléatoires ; angles min/max sans consommation retrouvée ; schéma DBG-L1 incertain.
- **Canonique** : [`niveaux-spawn-balles-et-physique.md`](../01-systemes/niveaux-spawn-balles-et-physique.md).

## 13. Balles et types de danger

- **Rôle — état** : unités physiques de collecte, score et danger ; **actif**.
- **Fonctionnement — cycle** : spawn → collisions/rebonds → bac ou vide → transformation éventuelle → résolution → recyclage.
- **Données — persistance** : quatre SO statiques ; état/type courant temporaire. Valeurs : white 100, blue 150, red 200, black -120.
- **Classes/assets** : `BallDefinition`, `BallDefinitionCatalog`, `BallState`, quatre assets `Ball_*`.
- **Entrantes → sortantes** : spawner/physique/modules → progression, score, coque, combos et FX.
- **Surface publique** : ID/type, points, danger, `countsForProgress`, changement de type.
- **Cas limites/validation** : noire exclue de la progression ; son effet dépend des transformations avant résolution.
- **Canonique** : [`niveaux-spawn-balles-et-physique.md`](../01-systemes/niveaux-spawn-balles-et-physique.md), [`equilibrage-et-configuration-active.md`](../02-donnees-et-unity/equilibrage-et-configuration-active.md).

## 14. Paddle, input et rebonds

- **Rôle — état** : contrôler le paddle et rediriger les balles ; gameplay **actif**, input **hybride**.
- **Fonctionnement — cycle** : lecture du contrôle, déplacement borné, collision et rebond personnalisé selon le point d’impact.
- **Données — persistance** : réglages prefab et tuning physiques sérialisés ; aucune position persistée.
- **Classes/assets** : `PlayerController`, `PlayerInputMouse`, `PlayerInputTouch`, `CursorController`, `BallPhysicsTuning`, prefab joueur.
- **Entrantes → sortantes** : souris/tactile/contrôles de niveau → transform/physique → balles et feedbacks.
- **Surface publique** : activation des contrôles, cible monde externe, lecture delta, callbacks de collision.
- **Cas limites/validation** : prefab en mode delta alors que des cibles externes existent ; voie tactile réelle non établie ; asset Input System générique peu consommé.
- **Canonique** : [`tutoriel-pause-input-et-plateformes.md`](../01-systemes/tutoriel-pause-input-et-plateformes.md), [`paddle-bacs-et-flush.md`](../01-systemes/paddle-bacs-et-flush.md).

## 15. Bacs, flush et transformations

- **Rôle — état** : collecter et résoudre un groupe de balles ; **actif**.
- **Fonctionnement — cycle** : collecte → `BinSnapshot` → transformations A/B → combos/score → conséquence des noires → événements → recyclage.
- **Données — persistance** : contenu vivant/snapshot temporaires ; score/coque résultants rejoignent l’état de niveau/run. Seuil automatique de base : 5, modifiable par GREED.
- **Classes/assets** : `BinCollector`, `BinTrigger`, `CloseBinController`, `BlackFilterRuntimeController`, `FlushResolutionEngine`.
- **Entrantes → sortantes** : balles et commandes clavier/tactile/seuil/fin → score, progression, coque, combos, UI/audio/FX.
- **Surface publique** : collecte, fermeture/flush, snapshot et événements de résolution.
- **Cas limites/validation** : une balle ne doit être résolue qu’une fois ; ordre des transformations avant dégâts ; ambiguïté de score de base.
- **Canonique** : [`paddle-bacs-et-flush.md`](../01-systemes/paddle-bacs-et-flush.md).

## 16. Physique et obstacles

- **Rôle — état** : faire circuler les balles et configurer le plateau ; **actif**.
- **Fonctionnement — cycle** : Rigidbody2D/colliders/triggers, grâce au plafond, murs/vide/bacs/paddle ; obstacles instanciés depuis les placements du niveau.
- **Données — persistance** : tuning, prefabs et placements statiques ; état physique temporaire.
- **Classes/assets** : `BallCeilingGrace`, `ObstacleManager`, `ObstaclePlacement`, `Obstacle1`, triggers et détecteurs d’impact.
- **Entrantes → sortantes** : contexte de niveau/tuning → collisions → trajectoires, collecte/perte et feedbacks.
- **Surface publique** : installation/cleanup des obstacles, callbacks de collision/trigger.
- **Cas limites/validation** : un seul type d’obstacle retrouvé ; `phaseIndex` sans consommation ; sensation/ordre physique non testés.
- **Canonique** : [`niveaux-spawn-balles-et-physique.md`](../01-systemes/niveaux-spawn-balles-et-physique.md).

## 17. Score

- **Rôle — état** : cumuler valeur de jeu et historique ; **actif**, calcul de base partiellement **incertain**.
- **Fonctionnement — cycle** : chaque flush fournit valeurs/combos/modificateurs au `ScoreManager`, qui met à jour total, historique et notifications ; fin intègre le résultat au run.
- **Données — persistance** : score/historique temporaires pendant le niveau ; score de run et snapshot persistés ; meilleur score permanent à la fin appropriée.
- **Classes/assets** : `ScoreManager`, `FlushResolution`, `FlushResolutionEngine`, `GameplayScoreImpactUI`, `ScoreAttractorUI`, `ScoreFlushAbsorberUI`.
- **Entrantes → sortantes** : balles, combos, modules → HUD, objectifs, fin, analytics et meilleur score.
- **Surface publique** : ajout/résolution, snapshot, événements de changement et historique de bacs.
- **Cas limites/validation** : `GetSnapshot` et `FinalTotal` semblent tous deux intégrer le base score ; double comptage réel non affirmé.
- **Canonique** : [`score-objectifs-et-combos.md`](../01-systemes/score-objectifs-et-combos.md), registre des [incertitudes](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## 18. Progression de mission

- **Rôle — état** : mesurer l’objectif principal de collecte ; **actif**.
- **Fonctionnement — cycle** : les balles admissibles résolues augmentent le compteur jusqu’à la cible ; l’évaluateur fige succès/échec en fin.
- **Données — persistance** : cible dans le JSON de niveau ; compteur temporaire ; résultat dans snapshot/run.
- **Classes/assets** : `ScoreManager`, `ProgressCountUI`, `ProgressBarUI`, `LevelResultEvaluator`, définition d’objectif principal.
- **Entrantes → sortantes** : flushs positifs et niveau → HUD puis résultat principal/fin.
- **Surface publique** : progression courante/cible, événements de compteur, évaluation finale.
- **Cas limites/validation** : les noires ne comptent pas ; distinguer progression, score et balles perdues.
- **Canonique** : [`score-objectifs-et-combos.md`](../01-systemes/score-objectifs-et-combos.md).

## 19. Objectifs secondaires

- **Rôle — état** : produire des critères additionnels et médailles ; **actif**.
- **Fonctionnement — cycle** : définitions chargées avec le niveau, suivi pendant la mission, évaluation après cleanup.
- **Données — persistance** : types/valeurs/phase dans JSON ; compteurs temporaires ; résultats dans snapshot.
- **Classes/assets** : `LevelSecondaryObjectivesController`, `SecondaryObjectivesManager`, modèles de résultat.
- **Entrantes → sortantes** : balles, combos, pertes et phases → résultat secondaire → médailles/récompenses/UI.
- **Surface publique** : enregistrement de métriques, résultats et événements de progression.
- **Cas limites/validation** : types observés BallCount, ComboCount, MaxCount, MaxLost et restriction de phase ; câblage exact par niveau à vérifier dans les JSON.
- **Canonique** : [`score-objectifs-et-combos.md`](../01-systemes/score-objectifs-et-combos.md).

## 20. Combos runtime

- **Rôle — état** : reconnaître motifs de couleur, timing, chaîne et volume lors des flushs ; **actif**, durée de vie statique **incertaine**.
- **Fonctionnement — cycle** : `ComboResolver` applique les règles au snapshot/résolution et met à jour les états de chaîne/timing.
- **Données — persistance** : définitions dans `ComboDefinitionCatalog.asset`; états runtime statiques, historique dans le résultat, pas de persistance de run établie.
- **Classes/assets** : `ComboResolver`, `ColorComboRule`, `TimingComboRule`, `ChainComboRule`, `VolumeComboRule`, `ChainRuntimeState`, `TimingRuntimeState`.
- **Entrantes → sortantes** : flush et temps → score, historique, overlays/audio et combos finaux.
- **Surface publique** : résolution, IDs de combo, événements de déclenchement, reset explicite dans tutoriel/debug.
- **Cas limites/validation** : reset normal entre niveaux non retrouvé ; catalogue et UI combo modifiés dans l’instantané Git.
- **Canonique** : [`score-objectifs-et-combos.md`](../01-systemes/score-objectifs-et-combos.md).

## 21. Bonus finaux et médailles

- **Rôle — état** : évaluer l’historique complet et traduire résultats secondaires en bonus/récompenses ; **actif avec incertitudes**.
- **Fonctionnement — cycle** : après nettoyage, `FinalComboEvaluator` et évaluateurs de résultats produisent lignes, médailles et bonus de modules.
- **Données — persistance** : `FinalComboConfig_Default.asset`, `EconomyConfig_Default.asset`; résultat final dans snapshot.
- **Classes/assets** : `FinalComboEvaluator`, `FinalComboConfig`, `LevelEndModuleBonusController`, `ModuleEndLevelBonusProvider`.
- **Entrantes → sortantes** : historique, objectifs, coque, modules → breakdown, argent éventuel, cérémonie et commit.
- **Surface publique** : évaluation finale et listes de bonus/médailles.
- **Cas limites/validation** : IDs `*FlushChain` face aux IDs runtime `*Chain`; `ptsJustInTime` absent de l’asset sérialisé observé.
- **Canonique** : [`score-objectifs-et-combos.md`](../01-systemes/score-objectifs-et-combos.md), [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md).

## 22. Fin de niveau et récompenses

- **Rôle — état** : arrêter proprement la mission, construire et appliquer le résultat ; chemin actuel **actif**, UI de fin **hybride**.
- **Fonctionnement — cycle** : arrêt contrôles/spawn → évacuation → flush final → cleanup → évaluation → outcome/snapshot → cérémonie → résultat → commit/sortie.
- **Données — persistance** : outcome temporaire ; snapshot/token persistés avant présentation ; score, argent, vies et progression appliqués au run au commit.
- **Classes/assets** : `LevelEndFlowController`, `EndSequenceController`, `LevelEvacuationController`, `EndLevelOutcomeBuilder`, `EndLevelSnapshot`.
- **Entrantes → sortantes** : timer/coque/abandon/objectifs → présentation, sauvegarde, analytics et navigation.
- **Surface publique** : demander une fin, construire résultat, préparer/commiter, sélectionner action de sortie.
- **Cas limites/validation** : anciennes familles d’UI restent sérialisées ; ne pas les confondre avec la cérémonie actuelle.
- **Canonique** : [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md).

## 23. Reprise de cérémonie et anti-double-commit

- **Rôle — état** : reprendre une fin interrompue et empêcher l’application répétée ; **actif statiquement**, runtime non exécuté.
- **Fonctionnement — cycle** : préparer token/snapshot → sauvegarder → présenter → vérifier token → appliquer une fois → effacer le pending.
- **Données — persistance** : `EndLevelToken`, `EndLevelSnapshot`, marqueur pending et état de commit dans `GameSaveData`.
- **Classes/assets** : `SaveManager`, `RunRecoveryOnBoot`, `EndSequenceController`, overlays de résultats.
- **Entrantes → sortantes** : interruption/rechargement/commande UI → reprise de présentation ou commit → run/navigation.
- **Surface publique** : prepare, resume, commit, clear ; gardes par token.
- **Cas limites/validation** : ordre des deux acteurs de récupération non confirmé ; ce mécanisme n’est pas un anti-cheat cryptographique.
- **Canonique** : [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md), [`sauvegarde-progression-et-integrite.md`](../01-systemes/sauvegarde-progression-et-integrite.md).

## 24. Tutoriel

- **Rôle — état** : guider W1-L1 une fois par profil ; **actif statiquement**.
- **Fonctionnement — cycle** : condition sur `tutorialComplete`, quatre étapes, spawns/interactions contrôlés, restauration d’état puis sauvegarde de complétion.
- **Données — persistance** : drapeau permanent ; état guidé et captures temporaires.
- **Classes/assets** : `LevelTutorialController`, références sérialisées de `Main`, contrôleurs/UI de tutoriel.
- **Entrantes → sortantes** : niveau W1-L1 + profil → contrôles, spawner, combos et UI → profil complété.
- **Surface publique** : start/advance/complete/restore ; événements d’étape.
- **Cas limites/validation** : reset des combos explicitement observé ici, mais pas établi pour les niveaux normaux ; séquence non jouée.
- **Canonique** : [`tutoriel-pause-input-et-plateformes.md`](../01-systemes/tutoriel-pause-input-et-plateformes.md).

## 25. Pause, abandon et retry

- **Rôle — état** : suspendre/reprendre et sortir d’une mission ; pause/abandon **actifs**, retry autonome **non établi**.
- **Fonctionnement — cycle** : overlay de pause suspend le temps, informe l’audio, reprend ou déclenche une sortie/abandon vers le flux de fin/récupération.
- **Données — persistance** : état de pause temporaire ; abandon/niveau en cours marqués en sauvegarde selon le chemin.
- **Classes/assets** : `LevelPauseFlowHandler`, `PauseOverlayController`, `LevelRunStateController`, `GameFlowController`.
- **Entrantes → sortantes** : input/UI → `Time`/contrôles/audio → reprise ou sauvegarde/navigation.
- **Surface publique** : pause, resume, exit/abandon ; action « retry » distincte non documentée comme système indépendant.
- **Cas limites/validation** : distinguer retry, rechargement et reprise après crash ; comportement exact doit être observé avant d’en parler comme fonctionnalité.
- **Canonique** : [`tutoriel-pause-input-et-plateformes.md`](../01-systemes/tutoriel-pause-input-et-plateformes.md), [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md).

## 26. HUD et overlays

- **Rôle — état** : présenter états et séquences, recueillir commandes ; **actif**, UI de score V2 **validée**, écrans de fin **hybrides**.
- **Fonctionnement — cycle** : binders/contrôleurs s’abonnent aux managers ; `MainUIController` orchestre briefing, countdown, pause, combos, dégâts, évacuation et résultats.
- **Données — persistance** : état de vue temporaire ; aucune autorité métier, sauf commandes transmises aux systèmes.
- **Classes/assets** : `MainUIController`, `GameplayScoreImpactUI`, `ScoreAttractorUI`, `ScoreFlushAbsorberUI`, `HullBinder`, `ContractLivesBinder`, overlays `ResultsCeremony`/`EndResult`, prefabs UI.
- **Entrantes → sortantes** : run/score/coque/timer/bacs → vues ; boutons → flow, boutique, équipement, pause et fin.
- **Surface publique** : show/hide/play/refresh, callbacks de boutons, abonnements aux événements.
- **Cas limites/validation** : chaîne score V2 câblée et validée en Play Mode, ancien chemin retiré ; anciennes UI de fin présentes ; leurs rendus restent à distinguer du chemin orchestré.
- **Canonique** : [`ui-hud-et-overlays.md`](../01-systemes/ui-hud-et-overlays.md), état [legacy/en cours](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).

## 27. Audio

- **Rôle — état** : musique globale/de niveau, SFX, crossfade, ducking et pause ; **actif statiquement**, lecteur de titre **hybride**.
- **Fonctionnement — cycle** : `AudioManager` persiste ; `LevelMusicDirector` choisit une paire briefing/gameplay et suit les phases du niveau.
- **Données — persistance** : IDs/clips et références sérialisées ; état de lecture temporaire, aucun réglage utilisateur persistant établi.
- **Classes/assets** : `AudioManager`, `LevelMusicDirector`, `ImpactSfxEmitter`, `MusicId`, `SfxId`.
- **Entrantes → sortantes** : scènes/UI/narration/collisions/pause → AudioSources et mix perçu.
- **Surface publique** : play SFX/music, transition/crossfade, duck/unduck, pause/resume.
- **Cas limites/validation** : `TitleMusicPlayer` sans instance retrouvée ; sélection musicale aléatoire ; mix/synchro non écoutés.
- **Canonique** : [`audio-localisation-et-dialogues.md`](../01-systemes/audio-localisation-et-dialogues.md).

## 28. Localisation et dialogues

- **Rôle — état** : résoudre textes UI/contenu et présenter la narration ; **actif mais hybride**.
- **Fonctionnement — cycle** : manager Boot charge domaine/langue ; narration choisit personnages et variantes pondérées, puis affiche frappe/portrait/SFX.
- **Données — persistance** : JSON `ui`, `ships`, `modules`, `dialogs` en/fr ; langue observée fixée à `fr`, pas de préférence persistante établie.
- **Classes/assets** : `LocalizationManager`, contrôleurs de narration, `CrewDatabase`, six SO de personnages.
- **Entrantes → sortantes** : clés et contexte de niveau → textes/dialogues/audio/UI.
- **Surface publique** : lookup de clé, fallback, sélection de variante, démarrage/skip de dialogue.
- **Cas limites/validation** : contenus anglais/français directs et fallbacks codés ; sélection pondérée non déterministe.
- **Canonique** : [`audio-localisation-et-dialogues.md`](../01-systemes/audio-localisation-et-dialogues.md).

## 29. Caméra, environnement et effets

- **Rôle — état** : cadrage et feedback visuel ; **actif comme présentation**, rendu non validé.
- **Fonctionnement — cycle** : scènes installent caméras/volumes ; événements de collision, menace et jeu déclenchent post-traitement, mouvements, trails et HUD feel.
- **Données — persistance** : réglages/prefabs/matériaux sérialisés ; états FX temporaires.
- **Classes/assets** : `AspectRatioKeeper`, `BackgroundScroller`, `ParallaxAsteroid`, `BlackThreatTracker`, `BlackThreatFXController`, contrôleurs GameFeel.
- **Entrantes → sortantes** : physique, bacs, coque, score, vaisseau → caméra, shaders, post-process, animations et vidéo.
- **Surface publique** : impulsions/impact/shake/contamination/threat, start/stop de vidéo.
- **Cas limites/validation** : ne possède pas l’état métier ; vidéo, timing, aspect mobile et rendu URP non contrôlés visuellement.
- **Canonique** : [`camera-environnement-fx-et-video.md`](../01-systemes/camera-environnement-fx-et-video.md).

## 30. Analytics et services externes

- **Rôle — état** : télémétrie alpha, bug report et liens ; **présent**, analytics désactivé en Editor et début de run **incertain**.
- **Fonctionnement — cycle** : événements `level_end`/`run_end` envoyés à un formulaire Google ; F8 ouvre un formulaire ; crédits ouvrent Discord.
- **Données — persistance** : métriques préparées depuis run/résultat ; aucune persistance distante autoritaire retrouvée.
- **Classes/assets** : `AlphaAnalytics`, `BugReportHotkey`, `CreditsController`, `CreditsCatalog.json`.
- **Entrantes → sortantes** : début/fin de run/niveau et actions UI → `UnityWebRequest` ou `Application.OpenURL`.
- **Surface publique** : begin/end analytics, report bug, open URL.
- **Cas limites/validation** : `BeginRun` exige l’index 0 alors que le premier niveau suit la boutique ; réseau jamais appelé pendant l’analyse.
- **Canonique** : [`analytics-credits-et-services-externes.md`](../01-systemes/analytics-credits-et-services-externes.md).

## 31. Debug et outils alpha

- **Rôle — état** : lancer, injecter, diagnostiquer et tester manuellement ; **présent**, plusieurs composants actifs dans `Main`.
- **Fonctionnement — cycle** : `DebugLauncher`/starter configurent un contexte ; installateur standalone remplace les dépendances Boot ; outils Editor ajoutent menus/inspecteurs.
- **Données — persistance** : `DBG-L1`, `DEBUG_SHIP`, paramètres injectés ; les chemins debug peuvent modifier la sauvegarde locale.
- **Classes/assets** : `MainDebugStarterV3`, `MainStandaloneInstaller`, `ComboSessionTester`, `ChainRuntimeDebugLogger`, `FinalComboTester`, `FlushTest`, `MissingScriptScanner`.
- **Entrantes → sortantes** : développeur/Editor/raccourcis → contexte, logs, UI debug, état local et `Main`.
- **Surface publique** : lancement/injection, scans, commandes Editor, raccourcis.
- **Cas limites/validation** : présence active ne prouve pas exécution ; aucun test automatisé projet retrouvé ; ces outils contournent les parcours normaux.
- **Canonique** : [`debug-et-outils-alpha.md`](../01-systemes/debug-et-outils-alpha.md).

## 32. Données, configuration et compilation

- **Rôle — état** : relier contenu, réglages Unity et code ; **actif**, schémas/identité partiellement hybrides.
- **Fonctionnement — cycle** : `Resources.Load` charge JSON/assets ; scènes/prefabs injectent références ; code calcule les contextes et applique constantes/fallbacks.
- **Données — persistance** : définitions statiques sous `Assets/Resources`/`Assets/Project/Data`; sauvegarde séparée dans PlayerPrefs.
- **Classes/assets** : services de catalogue, ScriptableObjects, `ProjectSettings`, `Packages/manifest.json`; Unity 6000.2.6f2, URP 17.2.0, Input System 1.14.2.
- **Entrantes → sortantes** : assets/import/références → tous les systèmes runtime.
- **Surface publique** : résolution par ID/chemin, références sérialisées et paramètres de projet.
- **Cas limites/validation** : aucun `.asmdef`, identité VoidScrappers/404 mixte, aucun build/import propre/test de compilation exécuté.
- **Canonique** : [`donnees-schemas-et-sources-de-verite.md`](../02-donnees-et-unity/donnees-schemas-et-sources-de-verite.md), [`reglages-unity-packages-et-compilation.md`](../02-donnees-et-unity/reglages-unity-packages-et-compilation.md).

## Limite de la référence

Cette page décrit les points structurants et surfaces de propagation, pas toutes les classes ni tous les UnityEvents sérialisés. Lorsque le dépôt est disponible, les documents canoniques et les sources citées doivent être relus avant de conclure. Pour la méthode de préparation et de validation d’un changement, utiliser [AI_WORKING_GUIDE_404.md](AI_WORKING_GUIDE_404.md).
