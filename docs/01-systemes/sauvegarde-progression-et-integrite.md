# Sauvegarde, progression et intégrité

> **Périmètre** : stockage local, profil permanent, état de run, reprise et protections transactionnelles.  
> **Statut** : confirmé par analyse du code ; résistance réelle non testée en exécution.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `SaveManager.cs`, `GameSaveData.cs`, `RunSessionState.cs`, `RunRecoveryOnBoot.cs`, `EndLevelToken.cs`, `EndLevelSnapshot.cs`.

## Stockage

`SaveManager` sérialise un `GameSaveData` JSON dans `PlayerPrefs`, sous la clé `GameSave_v1`. Une clé `VS_GAME_VERSION` mémorise la version applicative. Quand la version lue diffère de la version courante, le code appelle `PlayerPrefs.DeleteAll()` avant de recréer les données.

La sauvegarde est locale et lisible/modifiable par l’utilisateur. Aucun chiffrement, signature, HMAC, serveur d’autorité ou validation distante n’a été retrouvé. Le système de token de fin vise l’idempotence et la reprise, pas l’anti-triche au sens de sécurité hostile.

## Données permanentes

Le profil conserve notamment : identifiant de profil, vaisseau sélectionné, liste de vaisseaux débloqués, meilleur score de run et complétion du tutoriel. Le contenu de run (argent et modules compris) n’est pas une progression permanente.

Le mécanisme effectif de déblocage des vaisseaux n’est pas établi. Les éléments contradictoires sont décrits uniquement dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Données de run persistées

Le modèle conserve l’identifiant du run, monde/nœud/vaisseau, coque, vies de contrat, score et argent, ainsi que modules possédés/équipés, slots, offres de boutique, rerolls et bonus de coque maximale. Il porte aussi les marqueurs de niveau en cours/abandonné et la fin en attente avec son token et son snapshot.

`RunSessionState` reflète cet état en mémoire et notifie les consommateurs. La table canonique des propriétaires est dans [Données et sources de vérité](../02-donnees-et-unity/donnees-schemas-et-sources-de-verite.md).

## Écriture et reprise

- Les mutations structurantes du run passent par les services/états de run puis sont sauvegardées.
- L’entrée dans un niveau pose un marqueur de niveau en cours.
- La fin prépare un token et un snapshot avant l’animation de résultats.
- Le commit consomme une seule fois le résultat associé au token.
- Au Boot, une fin préparée peut être reprise depuis le snapshot ; un niveau resté en cours peut être compté comme abandonné.

Deux composants participent à la récupération. La répartition observée et l’ordre non vérifié sont centralisés dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Intégrité observée

Les protections sont fonctionnelles contre les doubles clics, rechargements de scène et interruptions dans la séquence de fin, grâce au token et aux marqueurs persistés. Elles ne protègent pas contre l’édition volontaire de `PlayerPrefs`, les chemins debug ou une modification du client.
