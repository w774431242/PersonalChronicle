# Installed runtime API index

Generated signatures; public does not imply a supported mod extension point.

## Verse.ModsConfig

```text
System.Collections.Generic.IEnumerable`1<Verse.ModMetaData> Verse.ModsConfig::get_ActiveModsInLoadOrder()
RimWorld.ExpansionDef Verse.ModsConfig::get_LastInstalledExpansion()
System.Boolean Verse.ModsConfig::get_RoyaltyActive()
System.Boolean Verse.ModsConfig::get_IdeologyActive()
System.Boolean Verse.ModsConfig::get_BiotechActive()
System.Boolean Verse.ModsConfig::get_AnomalyActive()
System.Boolean Verse.ModsConfig::get_OdysseyActive()
System.Boolean Verse.ModsConfig::TryGetPackageIdWithoutExtraSteamPostfix(System.String,System.String&)
System.Void Verse.ModsConfig::DeactivateNotInstalledMods(System.Action`1<System.String>)
System.Void Verse.ModsConfig::Reset()
System.Boolean Verse.ModsConfig::TryReorder(System.Int32,System.Int32,System.String&)
System.Void Verse.ModsConfig::Reorder(System.Collections.Generic.List`1<System.Int32>)
System.Boolean Verse.ModsConfig::IsActive(Verse.ModMetaData)
System.Boolean Verse.ModsConfig::AreAllActive(System.String)
System.Boolean Verse.ModsConfig::AreAllActive(System.Collections.Generic.IEnumerable`1<System.String>)
System.Boolean Verse.ModsConfig::IsAnyActiveOrEmpty(System.Collections.Generic.IEnumerable`1<System.String>,System.Boolean)
System.Boolean Verse.ModsConfig::IsActive(System.String)
System.Void Verse.ModsConfig::SetActive(Verse.ModMetaData,System.Boolean)
System.Void Verse.ModsConfig::SetActive(System.String,System.Boolean)
System.Void Verse.ModsConfig::EnsureModAdheresToForcedLoadOrder(System.String)
System.Void Verse.ModsConfig::SetActiveToList(System.Collections.Generic.List`1<System.String>)
System.Boolean Verse.ModsConfig::IsExpansionNew(System.String)
System.Void Verse.ModsConfig::AddKnownExpansion(System.String)
System.Void Verse.ModsConfig::Save()
System.Void Verse.ModsConfig::SaveFromList(System.Collections.Generic.List`1<System.String>)
System.Void Verse.ModsConfig::RestartFromChangedMods()
System.Collections.Generic.Dictionary`2<System.String,System.String> Verse.ModsConfig::GetModWarnings()
System.Boolean Verse.ModsConfig::ModHasAnyOrderingIssues(Verse.ModMetaData)
System.Void Verse.ModsConfig::TrySortMods()
```

## Verse.ModLister

```text
System.Collections.Generic.IEnumerable`1<Verse.ModMetaData> Verse.ModLister::get_AllInstalledMods()
System.Collections.Generic.IEnumerable`1<System.IO.DirectoryInfo> Verse.ModLister::get_AllActiveModDirs()
System.Collections.Generic.List`1<RimWorld.ExpansionDef> Verse.ModLister::get_AllExpansions()
System.Boolean Verse.ModLister::get_RoyaltyInstalled()
System.Boolean Verse.ModLister::get_IdeologyInstalled()
System.Boolean Verse.ModLister::get_BiotechInstalled()
System.Boolean Verse.ModLister::get_AnomalyInstalled()
System.Boolean Verse.ModLister::get_OdysseyInstalled()
System.Boolean Verse.ModLister::get_ShouldLogIssues()
System.Void Verse.ModLister::EnsureInit()
System.Void Verse.ModLister::RebuildModList()
System.Int32 Verse.ModLister::InstalledModsListHash(System.Boolean)
Verse.ModMetaData Verse.ModLister::GetModWithIdentifier(System.String,System.Boolean)
Verse.ModMetaData Verse.ModLister::GetActiveModWithIdentifier(System.String,System.Boolean)
RimWorld.ExpansionDef Verse.ModLister::GetExpansionWithIdentifier(System.String)
System.Boolean Verse.ModLister::HasActiveModWithName(System.String)
System.Boolean Verse.ModLister::AnyFromListActive(System.Collections.Generic.List`1<System.String>)
System.Boolean Verse.ModLister::AnyModActiveNoSuffix(System.Collections.Generic.List`1<System.String>)
System.Boolean Verse.ModLister::AnyModActiveNoSuffix(System.Collections.Generic.IEnumerable`1<System.String>)
System.Boolean Verse.ModLister::AllModsActiveNoSuffix(System.Collections.Generic.List`1<System.String>)
System.Boolean Verse.ModLister::AllModsActiveNoSuffix(System.Collections.Generic.IEnumerable`1<System.String>)
System.Boolean Verse.ModLister::CheckRoyalty(System.String)
System.Boolean Verse.ModLister::CheckIdeology(System.String)
System.Boolean Verse.ModLister::CheckBiotech(System.String)
System.Boolean Verse.ModLister::CheckAnomaly(System.String)
System.Boolean Verse.ModLister::CheckOdyssey(System.String)
System.Boolean Verse.ModLister::CheckIdeologyOrBiotech(System.String)
System.Boolean Verse.ModLister::CheckRoyaltyAndIdeology(System.String)
System.Boolean Verse.ModLister::CheckRoyaltyOrIdeology(System.String)
System.Boolean Verse.ModLister::CheckRoyaltyOrBiotech(System.String)
System.Boolean Verse.ModLister::CheckRoyaltyOrAnomaly(System.String)
System.Boolean Verse.ModLister::CheckBiotechOrAnomaly(System.String)
System.Boolean Verse.ModLister::CheckRoyaltyOrOdyssey(System.String)
System.Boolean Verse.ModLister::CheckRoyaltyOrIdeologyOrBiotech(System.String)
System.Boolean Verse.ModLister::CheckBiotechOrAnomalyOrOdyssey(System.String)
System.Boolean Verse.ModLister::CheckAnyExpansion(System.String)
```

## Verse.ModMetaData

```text
UnityEngine.Texture2D Verse.ModMetaData::get_PreviewImage()
UnityEngine.Texture2D Verse.ModMetaData::get_Icon()
System.String Verse.ModMetaData::get_FolderName()
System.IO.DirectoryInfo Verse.ModMetaData::get_RootDir()
System.Boolean Verse.ModMetaData::get_IsCoreMod()
System.Boolean Verse.ModMetaData::get_Active()
System.Void Verse.ModMetaData::set_Active(System.Boolean)
System.Boolean Verse.ModMetaData::get_VersionCompatible()
System.Boolean Verse.ModMetaData::get_MadeForNewerVersion()
RimWorld.ExpansionDef Verse.ModMetaData::get_Expansion()
System.String Verse.ModMetaData::get_Name()
System.String Verse.ModMetaData::get_ShortName()
System.String Verse.ModMetaData::get_Description()
System.String Verse.ModMetaData::get_AuthorsString()
System.Collections.Generic.IEnumerable`1<System.String> Verse.ModMetaData::get_Authors()
System.String Verse.ModMetaData::get_ModVersion()
System.String Verse.ModMetaData::get_Url()
System.UInt32 Verse.ModMetaData::get_SteamAppId()
System.Collections.Generic.List`1<System.Version> Verse.ModMetaData::get_SupportedVersionsReadOnly()
System.String Verse.ModMetaData::get_PreviewImagePath()
System.String Verse.ModMetaData::get_ModIconImagePath()
System.Boolean Verse.ModMetaData::get_Official()
Verse.ContentSource Verse.ModMetaData::get_Source()
System.String Verse.ModMetaData::get_PackageId()
System.String Verse.ModMetaData::get_PackageIdNonUnique()
System.String Verse.ModMetaData::get_PackageIdPlayerFacing()
System.Collections.Generic.List`1<Verse.ModDependency> Verse.ModMetaData::get_Dependencies()
System.Collections.Generic.List`1<System.String> Verse.ModMetaData::get_LoadBefore()
System.Collections.Generic.List`1<System.String> Verse.ModMetaData::get_LoadAfter()
System.Collections.Generic.List`1<System.String> Verse.ModMetaData::get_ForceLoadBefore()
System.Collections.Generic.List`1<System.String> Verse.ModMetaData::get_ForceLoadAfter()
System.Collections.Generic.List`1<System.String> Verse.ModMetaData::get_IncompatibleWith()
System.String Verse.ModMetaData::get_ModIconPath()
System.Collections.Generic.List`1<System.String> Verse.ModMetaData::UnsatisfiedDependencies()
System.Boolean Verse.ModMetaData::get_HadIncorrectlyFormattedVersionInMetadata()
System.Boolean Verse.ModMetaData::get_HadIncorrectlyFormattedPackageId()
System.Void Verse.ModMetaData::.ctor(System.String,System.Boolean)
System.Void Verse.ModMetaData::.ctor(Verse.Steam.WorkshopItem)
System.Void Verse.ModMetaData::UnsetPreviewImage()
System.Boolean Verse.ModMetaData::SamePackageId(System.String,System.Boolean)
System.Collections.Generic.List`1<Verse.LoadFolder> Verse.ModMetaData::LoadFoldersForVersion(System.String)
System.Boolean Verse.ModMetaData::get_OnSteamWorkshop()
System.Void Verse.ModMetaData::PrepareForWorkshopUpload()
System.Boolean Verse.ModMetaData::CanToUploadToWorkshop()
Steamworks.PublishedFileId_t Verse.ModMetaData::GetPublishedFileId()
System.Void Verse.ModMetaData::SetPublishedFileId(Steamworks.PublishedFileId_t)
System.String Verse.ModMetaData::GetWorkshopName()
System.String Verse.ModMetaData::GetWorkshopDescription()
System.String Verse.ModMetaData::GetWorkshopPreviewImagePath()
System.Collections.Generic.IList`1<System.String> Verse.ModMetaData::GetWorkshopTags()
System.IO.DirectoryInfo Verse.ModMetaData::GetWorkshopUploadDirectory()
Verse.Steam.WorkshopItemHook Verse.ModMetaData::GetWorkshopItemHook()
System.Collections.Generic.IEnumerable`1<Verse.ModRequirement> Verse.ModMetaData::GetRequirements()
System.Int32 Verse.ModMetaData::GetHashCode()
System.String Verse.ModMetaData::ToString()
System.String Verse.ModMetaData::ToStringLong()
```

## Verse.ModContentPack

```text
System.String Verse.ModContentPack::get_RootDir()
System.String Verse.ModContentPack::get_PackageId()
System.String Verse.ModContentPack::get_PackageIdPlayerFacing()
System.UInt32 Verse.ModContentPack::get_SteamAppId()
Verse.ModMetaData Verse.ModContentPack::get_ModMetaData()
System.String Verse.ModContentPack::get_FolderName()
System.String Verse.ModContentPack::get_Name()
System.Int32 Verse.ModContentPack::get_OverwritePriority()
System.Boolean Verse.ModContentPack::get_IsCoreMod()
System.Boolean Verse.ModContentPack::get_IsOfficialMod()
System.Collections.Generic.IEnumerable`1<Verse.Def> Verse.ModContentPack::get_AllDefs()
System.Collections.Generic.IEnumerable`1<Verse.PatchOperation> Verse.ModContentPack::get_Patches()
System.Collections.Generic.List`1<System.String> Verse.ModContentPack::AllAssetNamesInBundle(System.Int32)
KTrie.StringTrieSet Verse.ModContentPack::AllAssetNamesInBundleTrie(System.Int32)
System.Void Verse.ModContentPack::.ctor(System.IO.DirectoryInfo,System.String,System.String,System.Int32,System.String,System.Boolean)
System.Void Verse.ModContentPack::ClearDestroy()
Verse.ModContentHolder`1<T> Verse.ModContentPack::GetContentHolder()
System.Void Verse.ModContentPack::ReloadContent(System.Boolean)
System.Collections.Generic.IEnumerable`1<Verse.LoadableXmlAsset> Verse.ModContentPack::LoadDefs(System.Boolean)
System.Collections.Generic.Dictionary`2<System.String,System.IO.FileInfo> Verse.ModContentPack::GetAllFilesForMod(Verse.ModContentPack,System.String,System.Func`2<System.String,System.Boolean>,System.Collections.Generic.List`1<System.String>)
System.Collections.Generic.List`1<System.Tuple`2<System.String,System.IO.FileInfo>> Verse.ModContentPack::GetAllFilesForModPreserveOrder(Verse.ModContentPack,System.String,System.Func`2<System.String,System.Boolean>,System.Collections.Generic.List`1<System.String>)
System.Boolean Verse.ModContentPack::AnyContentLoaded()
System.Boolean Verse.ModContentPack::AnyNonTranslationContentLoaded()
System.Boolean Verse.ModContentPack::AnyTranslationsLoaded()
System.Void Verse.ModContentPack::ClearPatchesCache()
System.Void Verse.ModContentPack::ClearDefs()
System.Void Verse.ModContentPack::AddDef(Verse.Def,System.String)
System.String Verse.ModContentPack::ToString()
```

## Verse.LoadedModManager

```text
System.Collections.Generic.List`1<Verse.ModContentPack> Verse.LoadedModManager::get_RunningModsListForReading()
System.Collections.Generic.IEnumerable`1<Verse.ModContentPack> Verse.LoadedModManager::get_RunningMods()
System.Collections.Generic.List`1<Verse.Def> Verse.LoadedModManager::get_PatchedDefsForReading()
System.Collections.Generic.IEnumerable`1<Verse.Mod> Verse.LoadedModManager::get_ModHandles()
System.Void Verse.LoadedModManager::LoadAllActiveMods(System.Boolean)
System.Void Verse.LoadedModManager::InitializeMods()
System.Void Verse.LoadedModManager::LoadModContent(System.Boolean)
System.Void Verse.LoadedModManager::CreateModClasses()
System.Collections.Generic.List`1<Verse.LoadableXmlAsset> Verse.LoadedModManager::LoadModXML(System.Boolean)
System.Void Verse.LoadedModManager::ErrorCheckPatches()
System.Void Verse.LoadedModManager::ApplyPatches(System.Xml.XmlDocument,System.Collections.Generic.Dictionary`2<System.Xml.XmlNode,Verse.LoadableXmlAsset>)
System.Xml.XmlDocument Verse.LoadedModManager::CombineIntoUnifiedXML(System.Collections.Generic.List`1<Verse.LoadableXmlAsset>,System.Collections.Generic.Dictionary`2<System.Xml.XmlNode,Verse.LoadableXmlAsset>)
System.Void Verse.LoadedModManager::ParseAndProcessXML(System.Xml.XmlDocument,System.Collections.Generic.Dictionary`2<System.Xml.XmlNode,Verse.LoadableXmlAsset>,System.Boolean)
System.Void Verse.LoadedModManager::ClearCachedPatches()
System.Void Verse.LoadedModManager::ClearDestroy()
T Verse.LoadedModManager::GetMod()
Verse.Mod Verse.LoadedModManager::GetMod(System.Type)
T Verse.LoadedModManager::ReadModSettings(System.String,System.String)
System.Void Verse.LoadedModManager::WriteModSettings(System.String,System.String,Verse.ModSettings)
```

## Verse.PlayDataLoader

```text
System.Boolean Verse.PlayDataLoader::get_Loaded()
System.Void Verse.PlayDataLoader::LoadAllPlayData(System.Boolean)
System.Void Verse.PlayDataLoader::ClearAllPlayData()
System.Void Verse.PlayDataLoader::HotReloadDefs()
```

## Verse.DirectXmlLoader

```text
Verse.LoadableXmlAsset[] Verse.DirectXmlLoader::XmlAssetsInModFolder(Verse.ModContentPack,System.String,System.Collections.Generic.List`1<System.String>)
System.Collections.Generic.IEnumerable`1<T> Verse.DirectXmlLoader::LoadXmlDataInResourcesFolder(System.String)
T Verse.DirectXmlLoader::ItemFromXmlFile(System.String,System.Boolean)
T Verse.DirectXmlLoader::ItemFromXmlFile(RimWorld.IO.VirtualDirectory,System.String,System.Boolean)
T Verse.DirectXmlLoader::ItemFromXmlString(System.String,System.String,System.Boolean)
Verse.Def Verse.DirectXmlLoader::DefFromNode(System.Xml.XmlNode,Verse.LoadableXmlAsset)
System.Collections.Generic.IEnumerable`1<T> Verse.DirectXmlLoader::AllGameItemsFromAsset(Verse.LoadableXmlAsset)
```

## Verse.DirectXmlToObject

```text
System.Func`3<System.Xml.XmlNode,System.Boolean,System.Object> Verse.DirectXmlToObject::GetObjectFromXmlMethod(System.Type)
T Verse.DirectXmlToObject::ObjectFromXml(System.Xml.XmlNode,System.Boolean)
System.Boolean Verse.DirectXmlToObject::ValidateListNode(System.Xml.XmlNode,System.Xml.XmlNode,System.Type)
System.String Verse.DirectXmlToObject::InnerTextWithReplacedNewlinesOrXML(System.Xml.XmlNode)
```

## Verse.DirectXmlToObjectNew

```text
Verse.Def Verse.DirectXmlToObjectNew::DefFromNodeNew(System.Xml.XmlNode,Verse.LoadableXmlAsset)
Verse.DirectXmlToObjectNew/ParseValueAndSetFieldDelegate Verse.DirectXmlToObjectNew::GetFieldSetterForType(System.Type)
Verse.DirectXmlToObjectNew/ParseValueAndAddListItemDelegate Verse.DirectXmlToObjectNew::GetListItemAdderForType(System.Type)
Verse.DirectXmlToObjectNew/ParseValueAndReturnDefDelegate Verse.DirectXmlToObjectNew::GetDefParserForType(System.Type)
System.Reflection.FieldInfo Verse.DirectXmlToObjectNew::ResolveFieldForNode(System.Type,System.Xml.XmlNode,System.Xml.XmlNode)
System.Type Verse.DirectXmlToObjectNew::ResolveTypeForNode(System.Reflection.FieldInfo,System.Xml.XmlNode)
System.Type Verse.DirectXmlToObjectNew::ResolveTypeForNode(System.Type,System.Xml.XmlNode)
System.Xml.XmlNode Verse.DirectXmlToObjectNew::GetNodeOnlyChild(System.Xml.XmlNode)
System.Object Verse.DirectXmlToObjectNew::MakeInstanceOfTypeForEmptyNode(System.Type,System.Xml.XmlNode)
System.Boolean Verse.DirectXmlToObjectNew::ValidateMayRequires(System.String,System.String)
```

## Verse.XmlInheritance

```text
System.Void Verse.XmlInheritance::TryRegisterAllFrom(Verse.LoadableXmlAsset,Verse.ModContentPack)
System.Void Verse.XmlInheritance::TryRegister(System.Xml.XmlNode,Verse.ModContentPack)
System.Void Verse.XmlInheritance::Resolve()
System.Xml.XmlNode Verse.XmlInheritance::GetResolvedNodeFor(System.Xml.XmlNode)
System.Void Verse.XmlInheritance::Clear()
```

## Verse.DirectXmlCrossRefLoader

```text
System.Boolean Verse.DirectXmlCrossRefLoader::MistypedMayRequire(System.String)
System.Boolean Verse.DirectXmlCrossRefLoader::get_LoadingInProgress()
System.Void Verse.DirectXmlCrossRefLoader::RegisterObjectWantsCrossRef(System.Object,System.Reflection.FieldInfo,System.String,System.String,System.String,System.Type)
System.Void Verse.DirectXmlCrossRefLoader::RegisterObjectWantsCrossRef(System.Object,System.String,System.String,System.String,System.String,System.Type)
System.Void Verse.DirectXmlCrossRefLoader::RegisterObjectWantsCrossRef(System.Object,System.String,System.Xml.XmlNode,System.String,System.String,System.Type)
System.Void Verse.DirectXmlCrossRefLoader::RegisterListWantsCrossRef(System.Collections.Generic.List`1<T>,System.String,System.Object,System.String,System.String)
System.Void Verse.DirectXmlCrossRefLoader::RegisterDictionaryWantsCrossRef(System.Collections.Generic.Dictionary`2<K,V>,System.Xml.XmlNode,System.Object)
T Verse.DirectXmlCrossRefLoader::TryResolveDef(System.String,Verse.FailMode,System.Object)
System.Void Verse.DirectXmlCrossRefLoader::Clear()
System.Void Verse.DirectXmlCrossRefLoader::ResolveAllWantedCrossReferences(Verse.FailMode)
```

## Verse.DefDatabase`1

```text
System.Collections.Generic.IEnumerable`1<T> Verse.DefDatabase`1::get_AllDefs()
System.Collections.Generic.List`1<T> Verse.DefDatabase`1::get_AllDefsListForReading()
System.Int32 Verse.DefDatabase`1::get_DefCount()
System.Void Verse.DefDatabase`1::AddAllInMods()
System.Void Verse.DefDatabase`1::Add(System.Collections.Generic.IEnumerable`1<T>)
System.Void Verse.DefDatabase`1::Add(T)
System.Void Verse.DefDatabase`1::Clear()
System.Void Verse.DefDatabase`1::ClearCachedData()
System.Void Verse.DefDatabase`1::ResolveAllReferences(System.Boolean,System.Boolean)
System.Void Verse.DefDatabase`1::ErrorCheckAllDefs()
T Verse.DefDatabase`1::GetNamed(System.String,System.Boolean)
T Verse.DefDatabase`1::GetNamedSilentFail(System.String)
T Verse.DefDatabase`1::GetByShortHash(System.UInt16)
System.Void Verse.DefDatabase`1::InitializeShortHashDictionary()
T Verse.DefDatabase`1::GetRandom()
```

## Verse.Def

```text
Verse.TaggedString Verse.Def::get_LabelCap()
System.Void Verse.Def::.ctor()
System.Collections.Generic.IEnumerable`1<RimWorld.StatDrawEntry> Verse.Def::SpecialDisplayStats(RimWorld.StatRequest)
System.Void Verse.Def::ResolveReferences()
System.Collections.Generic.IEnumerable`1<System.String> Verse.Def::ConfigErrors()
System.Void Verse.Def::PostSetIndices()
System.Void Verse.Def::ClearCachedData()
System.String Verse.Def::ToString()
System.Void Verse.Def::ResolveDefNameHash()
System.Int32 Verse.Def::GetHashCode()
T Verse.Def::GetModExtension()
System.Boolean Verse.Def::HasModExtension()
System.Boolean Verse.Def::Equals(Verse.Def)
```

## Verse.ModLoadFolders

```text
System.Collections.Generic.List`1<Verse.LoadFolder> Verse.ModLoadFolders::FoldersForVersion(System.String)
System.Collections.Generic.List`1<System.String> Verse.ModLoadFolders::DefinedVersions()
System.Void Verse.ModLoadFolders::LoadDataFromXmlCustom(System.Xml.XmlNode)
System.Collections.Generic.List`1<System.String> Verse.ModLoadFolders::GetIssueList(Verse.ModMetaData)
System.Void Verse.ModLoadFolders::.ctor()
```

## Verse.LoadFolder

```text
System.Boolean Verse.LoadFolder::get_ShouldLoad()
System.Void Verse.LoadFolder::.ctor(System.String,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.String>,System.Collections.Generic.List`1<System.String>)
System.Boolean Verse.LoadFolder::Equals(Verse.LoadFolder)
System.Boolean Verse.LoadFolder::Equals(System.Object)
System.Int32 Verse.LoadFolder::GetHashCode()
```

## Verse.PatchOperation

```text
System.Boolean Verse.PatchOperation::Apply(System.Xml.XmlDocument)
System.Void Verse.PatchOperation::Complete(System.String)
System.Collections.Generic.IEnumerable`1<System.String> Verse.PatchOperation::ConfigErrors()
System.Void Verse.PatchOperation::.ctor()
```

## Verse.PatchOperationFindMod

```text
System.String Verse.PatchOperationFindMod::ToString()
System.Void Verse.PatchOperationFindMod::.ctor()
```

## Verse.LoadableXmlAsset

```text
System.String Verse.LoadableXmlAsset::get_FullFilePath()
System.Void Verse.LoadableXmlAsset::.ctor(System.String,System.String)
System.Void Verse.LoadableXmlAsset::.ctor(System.IO.FileInfo,Verse.ModContentPack)
System.String Verse.LoadableXmlAsset::ToString()
```

## RimWorld.ExpansionDef

```text
UnityEngine.Texture2D RimWorld.ExpansionDef::get_IconFromStatus()
UnityEngine.Texture2D RimWorld.ExpansionDef::get_Icon()
UnityEngine.Texture2D RimWorld.ExpansionDef::get_BackgroundImage()
System.Collections.Generic.List`1<UnityEngine.Texture2D> RimWorld.ExpansionDef::get_PreviewImages()
System.String RimWorld.ExpansionDef::get_StoreURL()
RimWorld.ExpansionStatus RimWorld.ExpansionDef::get_Status()
System.String RimWorld.ExpansionDef::get_StatusDescription()
System.Void RimWorld.ExpansionDef::PostLoad()
System.Collections.Generic.IEnumerable`1<System.String> RimWorld.ExpansionDef::ConfigErrors()
System.Void RimWorld.ExpansionDef::.ctor()
```

## RimWorld.ExpansionDefOf

```text
```

## RimWorld.ExpansionStatus

```text
```
