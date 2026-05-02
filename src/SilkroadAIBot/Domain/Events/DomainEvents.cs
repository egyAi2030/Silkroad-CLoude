using SilkroadAIBot.Domain.Entities;

namespace SilkroadAIBot.Domain.Events;

public record EntitySpawnedEvent(SREntity Entity);
public record EntityDespawnedEvent(uint UniqueID);
public record CharacterHpChangedEvent(uint CharacterUniqueID, uint HP, uint MaxHP, uint MP, uint MaxMP);
public record CharacterPositionChangedEvent(uint CharacterUniqueID, SRCoord Position);
public record SkillCastResultEvent(uint SkillID, uint TargetUID, bool Success);
public record KillConfirmedEvent(uint CharacterUniqueID, uint VictimUID);
public record WorldStateClearedEvent();
