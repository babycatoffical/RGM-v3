using ProjectMER.Features.Serializable;
using UnityEngine;

namespace RGM.Modes.Abilities.Epic;

//[Ability("업무 증가", "새로운 워크스테이션을 발 아래에 생성합니다. 모두가 사용할 수 있습니다. 길을 막으면 제재 대상입니다.", AbilityCategory.Epic, AbilityType.EPIC_ADDWORKSTATION)]
public class AddWorkstation : Ability
{
    private const float ForwardOffset = 1.5f;
    private const float DownwardOffset = 0.15f;

    public override void OnEnabled()
    {
        Vector3 forward = Owner.CameraTransform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 rayOrigin = Owner.Position + forward * ForwardOffset + Vector3.up;
        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100, (LayerMask)1))
            return;

        new SerializableWorkstation
        {
            IsInteractable = true,
            Position = hit.point + Vector3.down * DownwardOffset,
            Rotation = new Vector3(0, Owner.Rotation.eulerAngles.y, 0),
            Scale = Vector3.one
        }.SpawnOrUpdateObject();
    }

    public override void OnDisabled()
    {
    }
}