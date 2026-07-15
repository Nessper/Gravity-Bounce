# Paddle, bacs et flush

> **Périmètre** : contrôle du paddle, collecte dans les bacs, fermeture, seuil automatique et pipeline de résolution d’un flush.  
> **Statut** : pipeline A/B et cohérence des conséquences confirmés par code ; validation Play Mode à conserver.
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `PlayerController.cs`, `BinCollector.cs`, `BinTrigger.cs`, `CloseBinController.cs`, `BlackFilterRuntimeController.cs`, `FlushResolutionEngine.cs`, services runtime de modules.

## Paddle

`PlayerController` déplace le paddle dans les limites configurées et calcule un rebond personnalisé en fonction du point d’impact. Les voies de commande et leur statut exact sont décrits dans [Tutoriel, pause, input et plateformes](tutoriel-pause-input-et-plateformes.md), avec les ambiguïtés centralisées dans le registre d’incertitudes.

## Collecte

Chaque bac accumule les balles entrées par son trigger. `BinCollector` tient les comptes et peut produire un `BinSnapshot`. Les contrôleurs visuels et icônes suivent l’état ouvert/fermé et la contamination par les noires.

Un flush peut être demandé par le joueur/contrôle de fermeture, par le seuil automatique ou par la séquence de fin. Le seuil de base est cinq balles ; les modules GREED l’augmentent. Le flush final fait partie du cycle d’évacuation.

## Pipeline canonique d’un flush

1. Le bac fige son contenu dans un snapshot et verrouille immédiatement les billes concernées avec `collected`.
2. Les effets de modules résolvent les types admissibles dans un ordre unique : filtres A sur les noires, puis conversions B sur les blanches obtenues ou déjà présentes.
3. Ce snapshot résolu est la source de vérité du son, des FX, du score, des combos et des conséquences de coque.
4. Les billes noires restantes appliquent leur conséquence de coque.
5. Le `ScoreManager` reçoit le résultat et met à jour score, progression, pertes, historique et compteurs.
6. Les événements alimentent overlays, audio et feedbacks.
7. Les objets balle sont recyclés et le bac revient à l’état vide.

L’évaluation détaillée du score et des combos est canonique dans [Score, objectifs et combos](score-objectifs-et-combos.md). Les effets des familles de modules sont inventoriés dans [Économie, boutique et modules](economie-boutique-et-modules.md).

## Balles noires

Une noire n’ajoute pas de progression. Si elle n’a pas été transformée/neutralisée par un effet équipé, elle contribue à la perte et au dommage de coque.

La famille A réserve une charge lorsqu’une noire est dans un bac et lui applique un aperçu blanc. Cette réservation reste réversible : si la bille ressort avant le flush, elle redevient noire et la charge est rendue. Si elle reste jusqu’au flush, la charge est consommée et la bille est résolue comme blanche — puis éventuellement améliorée par la famille B. Pendant cette réservation, les FX de menace et la contamination du bac suivent également l’état visuel transformé.

`BlackFilterRuntimeController` gère les charges de conversion associées.

## Arbitrage entre K1 et un flush

K1 et le snapshot partagent `BallState.collected` comme verrou terminal. L’ordre d’exécution détermine sans double résolution quel système devient propriétaire de la noire :

- si le snapshot verrouille la noire en premier, K1 échoue pendant sa phase silencieuse et aucun laser n’apparaît ;
- si K1 la verrouille en premier, il la retire du `BinTrigger` avant d’afficher le laser ; le snapshot ne la contient donc pas ;
- le retrait passe par `TryRemoveForNeutralization`, met à jour la liste ordonnée, les aperçus A/B, `inBin`/`currentSide` et `OnContentChanged` ;
- un auto-flush déjà programmé revérifie son seuil avant le snapshot et s’annule si le retrait K1 fait repasser le bac sous ce seuil ;
- un flush forcé ou final traite normalement les autres billes restantes.

Une noire déjà réservée par la famille A reste prioritaire pour A et n’est jamais ciblée par K1. Une fois le laser K1 visible, la neutralisation est garantie ; la bille ne peut plus rejoindre un bac, le vide ou un snapshot comme nouvelle résolution.

## Incertitude de score

Une ambiguïté existe sur l’intégration du score de base dans le parcours actif. Le constat technique n’est décrit qu’une fois dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).
