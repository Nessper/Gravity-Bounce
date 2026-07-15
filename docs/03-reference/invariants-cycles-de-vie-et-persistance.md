# Invariants, cycles de vie et persistance

> **Périmètre** : règles structurelles à préserver mentalement pour comprendre le runtime actuel.  
> **Statut** : invariants déduits de garde-fous explicites ; exceptions incertaines référencées.  
> **Date de vérification** : 2026-07-15.  
> **Principaux appuis** : services Boot, `RunSessionState`, `SaveManager`, contrôleurs de niveau/fin, systèmes de bac et modules.

## Cycle global

- Le root Boot doit exister une seule fois et survivre aux changements de scène.
- Un parcours normal possède un profil chargé avant la création/continuation du run.
- `Main` doit disposer d’un `LevelContext`, issu du nœud courant ou d’une injection debug/standalone.
- Les catalogues statiques sont résolus par identifiant ; le run persiste ces identifiants, pas des instances Unity.

## Run

- La coque courante est bornée par le maximum calculé du vaisseau et de ses bonus.
- Les vies de contrat sont distinctes de la coque.
- Argent, modules et score de run sont transitoires ; meilleur score et tutoriel appartiennent au profil permanent.
- Une offre générée est persistée jusqu’à achat/reroll/changement de boutique prévu.
- Un module équipé doit correspondre à un module possédé et à un slot du run selon les services d’équipement.

## Niveau et bac

- Une balle ne doit être résolue qu’une fois par collecte/flush avant recyclage.
- Le snapshot de bac est la frontière entre contenu physique et calcul de flush.
- Les transformations A puis B produisent un snapshot de flush unique ; son, FX, score, combos et conséquence finale des noires doivent tous en dériver.
- Une noire ne compte pas pour la progression principale.
- Une noire neutralisée par K1 repasse par le pool exactement une fois, sans score, dégât, collecte ou perte enregistrée.
- `BallState.collected` est un verrou de propriété terminal partagé : un snapshot ou K1 peut l’acquérir, mais une bille déjà verrouillée ne peut pas être reprise par l’autre système.
- Avant le laser visible, K1 reste annulable ; dès que le laser est affiché, la noire est retirée de son éventuel bac, le cooldown est consommé et la neutralisation doit aboutir, même lors de la fermeture du gameplay.
- Une noire réservée par la famille A ne doit jamais être réservée par K1.
- K0 modifie tous les drones uniquement par le contrat de `DroneRuntimeControllerBase` : départ chargé et multiplicateur de cooldown ; il ne crée pas de drone et ne connaît aucune classe concrète.
- Une bille réservée par K2 est temporairement exclue des bacs, du Void, de K1 et des autres drones ; son collider et sa simulation sont restaurés lors de la libération ou de l’annulation.
- K2 ne peut réserver ni une bille déjà collectée, ni une bille dans un bac, ni une bille de tutoriel, ni une bille déjà possédée par un autre système de drone.
- Une interruption de K2 ne doit jamais laisser une bille invisible, cinématique ou sans collider. Le nettoyage final force la libération des exclusions de drone résiduelles avant de reprendre ses règles normales.
- Le nettoyage final précède l’évaluation complète et le snapshot de fin.

## Transaction de fin

- Une fin préparée possède un token et un snapshot persistés avant sa consommation.
- Le résultat présenté provient du snapshot, afin d’être rejouable après interruption.
- Un même token ne doit être commité qu’une fois.
- Après commit, l’état de fin en attente est effacé et le run pointe vers son nouvel état.

## Durées de vie statiques

`ChainRuntimeState` et `TimingRuntimeState` sont statiques. Leur reset régulier de mission n’a pas été retrouvé ; ils constituent donc une exception potentielle à l’isolation attendue d’un niveau. Ce point reste **incertain** et n’est pas transformé ici en invariant.

## Frontières de confiance

Les invariants sont imposés par le client local et ses gardes. Les chemins debug, l’édition de PlayerPrefs ou une modification du client peuvent les contourner. La page [Sauvegarde, progression et intégrité](../01-systemes/sauvegarde-progression-et-integrite.md) décrit précisément cette limite.
