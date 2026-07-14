# Prefabs, ScriptableObjects et assets runtime

> **Périmètre** : familles d’assets Unity structurants, leur rôle et leur mode de résolution.  
> **Statut** : inventaire statique ; pas de validation d’import/rendu.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `Assets/Project/Prefabs`, `Assets/Project/Data`, `Assets/Resources`, GUID et références des scènes.

## ScriptableObjects structurants

- Balles : quatre `BallDefinition` et `BallDefinitionCatalog`.
- Combos : `ComboDefinitionCatalog`.
- Économie : `EconomyConfig_Default`.
- Combos finaux : `FinalComboConfig_Default`.
- Narration : six définitions de personnages et `CrewDatabase`.
- Run : `RunSessionState.asset`, état mutable runtime réinitialisé/synchronisé par les services.
- UI : `FinalComboStyleProvider` sous `Resources/UI`.

Ces assets définissent du contenu/configuration ou un conteneur runtime. Leurs valeurs fonctionnelles sont documentées dans les pages système et [Équilibrage](equilibrage-et-configuration-active.md).

## Prefabs

Les 51 prefabs observés couvrent principalement Boot/configuration, balles, paddle/plateau, obstacles, éléments d’environnement, HUD, overlays, boutique et interfaces de résultats. Les scènes détiennent leurs instances/références et certains services instancient les prefabs à la demande.

`PlayerOld.prefab` est présent mais n’a pas été retrouvé comme prefab joueur du chemin courant. De nouveaux prefabs de score étaient non suivis lors de l’analyse ; leur inventaire canonique se trouve dans [Travaux en cours](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Assets chargés par Resources

Les catalogues JSON, localisations, icônes/images de modules et vaisseaux, médias référencés et certains SO sont accessibles par chemin logique. Un asset présent sous `Resources` n’est pas nécessairement consommé : la consommation est confirmée par les services et références listés dans [Identifiants et chemins](../03-reference/identifiants-chemins-resources-et-api.md).

## Contrôle des références

Aucune référence littérale `m_Script: {fileID: 0}` n’a été trouvée dans les scènes/prefabs inspectés. Des `m_EditorClassIdentifier` anciens subsistent après des renommages, ce qui témoigne de l’historique mais ne prouve pas une référence cassée. `MissingScriptScanner` est présent dans Main pour un contrôle runtime/debug.
