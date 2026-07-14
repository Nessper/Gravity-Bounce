# Caméra, environnement, FX et vidéo

> **Périmètre** : cadrage, arrière-plans, mouvements décoratifs, feedbacks visuels, menace noire, shaders et vidéos.  
> **Statut** : composants/câblage observés ; qualité visuelle et timing non vérifiés en rendu.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : scripts `Camera`, `Environment`, `FX`, `UI/HUD/GameFeel`, composants vidéo des scènes Title/RunHub/Main.

## Cadrage et environnement

`AspectRatioKeeper` conserve le ratio visé par le jeu. Les arrière-plans utilisent `BackgroundScroller` et `ParallaxAsteroid`; `ShipBackgroundController` relie le vaisseau sélectionné à sa représentation de fond/intérieur. Les scènes portent les caméras et volumes de post-traitement URP.

## Menace des balles noires

`BlackThreatTracker` agrège la présence/importance des balles noires. `BlackThreatFXController` traduit cet état en effets de post-traitement. `BinContaminationVisualController` représente séparément la contamination d’un bac. Ces composants présentent la menace ; le dommage fonctionnel reste dans le pipeline de flush.

## Game feel

Les familles de feedbacks retrouvées couvrent :

- impacts et mouvements des bacs ;
- anneaux, idle et impacts d’obstacles ;
- mouvement, flash shader et smear du paddle ;
- sway moteur, idle, flush, impacts et afterimages du vaisseau ;
- impacts de murs d’énergie et screen shake ;
- traînées de balles, variation de largeur et grâce au plafond ;
- impulsions, scan, chemin lumineux et verre du HUD.

Les détecteurs émettent des signaux de présentation à partir des collisions ou événements du gameplay. Aucun de ces effets n’a été identifié comme source canonique de score ou d’état de run.

## Vidéo

Le titre utilise une boucle vidéo pilotée manuellement (`TitleVideoManualLoop`). D’autres composants vidéo participent aux transitions/ambiances selon les scènes. La lecture dépend des assets et du `VideoPlayer`; aucun contrôle visuel ou audio de ces médias n’a été effectué.
