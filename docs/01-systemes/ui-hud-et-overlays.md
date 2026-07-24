# UI, HUD et overlays

> **Périmètre** : architecture des interfaces, écrans, HUD de mission, overlays, boutique et systèmes de vaisseau.  
> **Statut** : inventaire confirmé par scripts/scènes ; chemin visuel du score validé en Play Mode, attente du flush final confirmée statiquement.
> **Date de vérification** : 2026-07-23.
> **Principaux appuis** : scripts sous `Assets/Project/Scripts/UI`, scènes de build et prefabs sous `Assets/Project/Prefabs`.

## Écrans hors mission

- `Title` : logo, vidéo et actions de départ.
- `ShipSelect` : choix visuel du vaisseau et transition vers le run.
- `RunHub` : carte de nœuds, statut du vaisseau, boutique, réparation et systèmes/modules.
- `CreditsScene` : lecture du catalogue de crédits et navigation externe associée.

Les contrôleurs de scène appellent le flux global ; les données métier restent détenues par le run, la boutique et les catalogues.

## HUD de mission

Le HUD expose l’identifiant de niveau, timer, score, progression, objectif, coque, vies de contrat, guides de paddle/bac et notifications de statistiques. `HullBinder` et `ContractLivesBinder` relient leurs états aux vues spécialisées. Pour le score, `GameplayScoreImpactUI` reçoit les impacts visuels, pilote l’affichage `AnimatedIntText` et se resynchronise sur la valeur autoritaire de `ScoreManager`. Plusieurs barres segmentées et groupes de taille TMP harmonisent la présentation.

## Overlays du chemin actif

`MainUIController` coordonne principalement : briefing, compte à rebours, tutoriel, pause, combos, dégâts, évacuation, cérémonie de résultats, résultat final et transition de sortie. Les overlays de combo runtime et de flush consomment les événements du moteur de score ; ils ne calculent pas le résultat canonique.

Les indicateurs de chaînes restent affichés jusqu’à la fin du flush final et de ses impacts visuels, puis sont effacés par le reset runtime commun. Leur barre de progression interpole sa cible entre deux paliers et se bloque pleine au niveau maximal.

Le détail du cycle de fin est dans [Fin de niveau](fin-de-niveau-recompenses-et-reprise.md). Les écrans historiques qui coexistent sont classés dans [Systèmes actifs, legacy et hybrides](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md).

## UI de boutique et d’équipement

Les vues `RunHubModules*` affichent les offres, achats et rerolls. Les contrôleurs `ShipSystems*` affichent modules possédés, équipés et slots, puis transmettent les interactions aux services de run. `ShipStatusPanelUI` fournit une représentation partagée du vaisseau.

Les transitions vers/depuis `Ship Systems` publient des événements explicites afin de vider les sélections des vues masquées par `CanvasGroup`. L’inventaire surligne le premier slot libre lorsqu’un module non équipé est sélectionné ; un clic sur ce slot ou un double-clic sur le module l’équipe. Après achat, le contour `Frame` et le texte du bouton `Tuning` pulsent en vert fluo, puis retrouvent leurs couleurs initiales à l’arrêt.

Sur le chemin de défaite directe, le HUD gameplay est masqué, le fond rejoint progressivement une opacité complète, puis `EndResult` apparaît. `EndResultOverlayController.PrepareForReveal()` nettoie l’ancien contenu avant le fondu du conteneur afin d’éviter l’apparition furtive d’un écran précédent.

## Présentation du score pendant un flush

Le chemin V2 est raccordé dans `Main` sans remplacer la chaîne métier. `FlushComboOverlayController` reçoit une seule demande de présentation par résolution, crée les paquets de billes et de combos, puis les confie à `ScoreAttractorUI`. Les paquets portent directement leur valeur numérique et leur couleur ; aucun texte n’est parsé pour reconstruire des points.

`ScoreAttractorUI` convertit la position issue du Canvas World Space vers `ScreenSpaceTransferRoot`, sous le Canvas Screen Space Overlay, puis anime une trajectoire accélérée et courbe. `ScoreFlushAbsorberUI` réserve les créneaux d’arrivée afin de produire une rafale lisible. `GameplayScoreImpactUI` joue le punch et le son. L'événement de `ScoreManager` mémorise immédiatement la dernière valeur autoritaire, tandis que chaque paquet effectivement arrivé avance la cible de présentation avec sa valeur numérique. Cette cible intermédiaire reste décorative, bornée vers la valeur autoritaire puis resynchronisée sur `ScoreManager` à la fin de la rafale, lors d’une fermeture du HUD ou à l’expiration du délai maximal du flush final.

`GameplayScoreImpactUI` regroupe aussi les valeurs numériques des paquets effectivement arrivés dans une session visuelle indépendante des flushs. Chaque impact relance un délai d’inactivité en temps de jeu, réglé à `1,5 s` dans `Main` pour permettre aux flushs rapprochés, doubles bacs et Fast Flush d’alimenter le même total. À son expiration, une somme décorative non nulle se détache du score, monte avec ralentissement, flotte, puis disparaît. Sa taille et sa couleur utilisent deux progressions normalisées et plafonnées séparément : une `AnimationCurve` interpole les échelles minimale et maximale, tandis qu’un `Gradient` fournit directement la couleur positive ; la couleur négative reste un réglage dédié. Les durées de montée, flottement et fade ainsi que la distance sont sérialisées. Cette somme ne lit jamais le texte du HUD et n’alimente aucun calcul métier. Lors du flush final, la session est finalisée dès que toutes les arrivées sont terminées ; la séquence de fin attend ensuite la disparition du total flottant avant l’outro du plateau et le masquage du HUD. Un garde-fou non dépendant de `timeScale`, réglé à `3,5 s` dans `Main`, resynchronise et nettoie les vues si cette présentation reste bloquée. Une fermeture du HUD ou un changement de scène supprime la session et les vues encore actives sans produire de total tardif.

Le nombre du score HUD utilise le mode optionnel `MechanicalOdometer` d'`AnimatedIntText`. L'événement autoritaire de `ScoreManager` mémorise la cible mais ne déclenche pas le roulement d'un flush : `GameplayScoreImpactUI` transmet les cibles intermédiaires à l'odomètre au contact effectif des paquets avec le HUD. Une mise à jour autoritaire sans séquence visuelle utilise une resynchronisation de secours différée. Les unités tournent avec la progression de la valeur, puis entraînent les dizaines et les centaines au passage de leurs retenues. Un nouvel impact pendant un roulement retargete immédiatement les roues depuis leur position visuelle courante ; les séquences qui se chevauchent, notamment le Fast Flush, restent donc ponctuées par leurs propres paquets sans constituer une longue file d'animations. Chaque roue défile verticalement dans un masque, avec durée, distance, courbe et fenêtre de prise de retenue réglables dans l'Inspector. Le mode historique `NumericLerp` reste la valeur par défaut des autres compteurs et écrans de fin. L'odomètre est une vue pure : il ne calcule, ne persiste et ne publie aucun score métier.

La taille des paquets pendant leur transfert est ajustable séparément pour les billes et les combos dans `ScoreAttractorUI`, sans modifier leur valeur ni leur destination.

La première itération visuelle de ce chemin est validée en Play Mode. L’ancien chemin `ScoreBinder`/`ScoreUI` et le prefab inutilisé `ScoreImpactPacketUI` ont été retirés après validation de la V2. `Main` ne conserve plus de référence sérialisée vers ces éléments.

## Frontière

Les animations, sons de bouton et feedbacks ne sont pas sources de vérité métier. L’UI lit les snapshots, états ou événements et déclenche des commandes de navigation/achat/équipement. Les exceptions ou doublons historiques sont documentés comme tels, sans recommandation d’évolution.
