﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CoverSensorsData
{
    public Transform currentCover { get; set; }
    public Transform leftSensor, rightSensor, coverHelper;
}

[System.Serializable]
public class BodyData
{
    public Transform bodyAimPivot, mainCrossHair;
    public float movingDamp, movingDeltaTime;
    public float jumpForce;
    public float distanceToGround;
    [HideInInspector]
    public Vector3 bodyAimPivotPosition;
}

[System.Serializable]
public class WeaponHolder
{
    public Gun gun;
    public GameObject sword;
    public Transform gunPlace_In;
    public Transform gunPlace_Out;
    public Transform swordPlace_In;
    public Transform swordPlace_Out;
    

    public void SwitchWeapons()
    {
        if (PlayerInput.Melee)
        {
            SetGunOut();
            SetSwordIn();
        }
        else
        {
            SetSwordOut();
            SetGunIn();
        }
    }

    private void SetGunIn()
    {
        gun.transform.parent = gunPlace_In.parent;
        gun.transform.localPosition = gunPlace_In.localPosition;
        gun.transform.localRotation = gunPlace_In.localRotation;
    }

    private void SetGunOut()
    {
        gun.transform.parent = gunPlace_Out.parent;
        gun.transform.localPosition = gunPlace_Out.localPosition;
        gun.transform.localRotation = gunPlace_Out.localRotation;
    }

    private void SetSwordIn()
    {
        sword.transform.parent = swordPlace_In.parent;
        sword.transform.localPosition = swordPlace_In.localPosition;
        sword.transform.localRotation = swordPlace_In.localRotation;
    }

    private void SetSwordOut()
    {
        sword.transform.parent = swordPlace_Out.parent;
        sword.transform.localPosition = swordPlace_Out.localPosition;
        sword.transform.localRotation = swordPlace_Out.localRotation;
    }
}

public class PlayerController : MonoCached
{
    [SerializeField] private WeaponHolder weaponHolder;
    [SerializeField] private BodyData bodyData;
    [SerializeField] private CoverSensorsData coverSens;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rigidbody;
    [SerializeField] private CapsuleCollider collider;
    [SerializeField] private AimingOverrider aimingHands;
    
    private static bool isGrounded;
    public static bool IsGrounded
    {
        get { return isGrounded; }
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PlayerInput.OnJumped += Jump;
        PlayerInput.OnSwordAttacked += SwordAttack;
        PlayerInput.OnWeaponSwitched += weaponHolder.SwitchWeapons;
    }

    private void Start()
    {
        SetUpAnimator();
        SetUpHands();
    }

    public void SetUpAnimator()
    {
        animator.SetFloat(AnimatorHashes.CoverSideHash, 1);
    }

    public void SetUpHands()
    {
        aimingHands.overridedChest.target = bodyData.mainCrossHair;
        aimingHands.overridedChest.weapon = weaponHolder.gun.transform;
        aimingHands.characterAnimator = animator;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(Tags.Cover))
        {
            coverSens.currentCover = other.transform;
            animator.SetBool(AnimatorHashes.CoverHash, true);
            if (PlayerInput.Horizontal == 0)
            {
                return;
            }
            animator.SetFloat(AnimatorHashes.CoverSideHash, PlayerInput.Horizontal > 0 ? 1 : -1);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(Tags.Cover))
        {
            animator.SetBool(AnimatorHashes.CoverHash, false);
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if(PlayerInput.Aiming)
        {
            AimingTransforms();
        }
        else
        {
            SimpleWalkingTransforms();
        }
    }

    public override void CustomFixedUpdate()
    {
        CheckIsGrounded();
        SetInputsToAnimator();
        Rotate();
        UpdateBodyAimPivot();

        if(PlayerInput.Firing)
        {
            Fire();
        }

        if(animator.GetBool(AnimatorHashes.CoverHash))
        {
            CoverTransforms();
        }
    }
    
    private void CheckIsGrounded()
    {
        if (Physics.Raycast(transform.up, -transform.up, bodyData.distanceToGround))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }

    private void SetInputsToAnimator()
    {
        animator.SetBool(AnimatorHashes.MeleeHash, PlayerInput.Melee);
        animator.SetBool(AnimatorHashes.AimingHash, PlayerInput.Aiming);
        animator.SetBool(AnimatorHashes.CrouchingHash, PlayerInput.Crouching);
        animator.SetBool(AnimatorHashes.ShiftingHash, PlayerInput.Shifting);
        animator.SetFloat(AnimatorHashes.VerticalHash, PlayerInput.Vertical, bodyData.movingDamp, Time.fixedDeltaTime * bodyData.movingDeltaTime);
        animator.SetFloat(AnimatorHashes.HorizontalHash, PlayerInput.Horizontal, bodyData.movingDamp, Time.fixedDeltaTime * bodyData.movingDeltaTime);
        animator.SetFloat(AnimatorHashes.Mouse_YHash, PlayerInput.MouseY); 
    }

    private void Jump()
    {
        HandleRootMotionOnJump();
        HandleColliderBoundsOnJump();
        animator.SetTrigger(AnimatorHashes.Jumphash);
        rigidbody.AddForce((transform.up + transform.forward) * bodyData.jumpForce, ForceMode.VelocityChange);
    }

    private void HandleRootMotionOnJump()
    {
        animator.applyRootMotion = false;
        StartCoroutine(delayedRootMotionEnable());
        IEnumerator delayedRootMotionEnable()
        {
            yield return new WaitForSeconds(1f);
            animator.applyRootMotion = true;
        }
    }

    private void HandleColliderBoundsOnJump()
    {
        collider.height = 0.2f;
        collider.center = Vector3.up*3;
        StartCoroutine(delayedColliderReset());
        IEnumerator delayedColliderReset()
        {
            yield return new WaitForSeconds(1f);
            
            collider.height = 1.6f;
            collider.center = Vector3.up;
        }
    }

    private void SwordAttack()
    {
        animator.SetTrigger(AnimatorHashes.SwordAttackHash);
    }

    private void Rotate()
    {
        transform.rotation *= Quaternion.AngleAxis(PlayerInput.MouseX * 15f, transform.up);
    }

    private void UpdateBodyAimPivot()
    {
        bodyData.bodyAimPivotPosition.y += PlayerInput.MouseY;
        bodyData.bodyAimPivotPosition.y = Mathf.Clamp(bodyData.bodyAimPivotPosition.y, -0.7f, 3.5f);
        bodyData.bodyAimPivotPosition.z = 5f;
        bodyData.bodyAimPivot.localPosition = bodyData.bodyAimPivotPosition;
    }

    public void CoverTransforms()
    {
        if(animator.GetFloat(AnimatorHashes.VerticalHash) < -0.7f)
        {
            return;
        }
        if (!animator.GetBool(AnimatorHashes.AimingHash) && animator.GetBool(AnimatorHashes.CrouchingHash))
        {
            RaycastHit hitCover;
            Debug.DrawRay(coverSens.rightSensor.position, coverSens.rightSensor.forward, Color.blue);
            Debug.DrawRay(coverSens.leftSensor.position, coverSens.leftSensor.forward, Color.blue);

            if (animator.GetFloat(AnimatorHashes.CoverSideHash) > 0)
            {
                if (Physics.Raycast(coverSens.rightSensor.position, coverSens.rightSensor.forward, out hitCover, 2f, 1<<10))
                {
                    coverSens.coverHelper.position = hitCover.point - coverSens.coverHelper.forward;
                    coverSens.currentCover = hitCover.transform;
                    coverSens.coverHelper.rotation = Quaternion.Lerp(coverSens.coverHelper.rotation, Quaternion.LookRotation(-hitCover.normal), Time.deltaTime * 10f);
                }
                else
                {
                    animator.SetFloat(AnimatorHashes.CoverSideHash, 2f);
                }
            }
            if (animator.GetFloat(AnimatorHashes.CoverSideHash) < 0)
            {
                if (Physics.Raycast(coverSens.leftSensor.position, coverSens.leftSensor.forward, out hitCover, 2f, 1 << 10))
                {
                    coverSens.coverHelper.position = hitCover.point - coverSens.coverHelper.forward;
                    coverSens.currentCover = hitCover.transform;
                    coverSens.coverHelper.rotation = Quaternion.Lerp(coverSens.coverHelper.rotation, Quaternion.LookRotation(-hitCover.normal), Time.deltaTime * 10f);
                }
                else
                {
                    animator.SetFloat(AnimatorHashes.CoverSideHash, -2f);
                }
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-coverSens.currentCover.forward), Time.deltaTime * 2f);
        }
    }

    public void AimingTransforms()
    {
        PlayerCameraBehaviour.FieldOfView(35f);

        if ((bodyData.mainCrossHair.position - weaponHolder.gun.transform.position).magnitude > 2f)
        {
            animator.SetLookAtWeight(0.5f, 1f, 1f);
            animator.SetLookAtPosition(bodyData.mainCrossHair.position);
        }
    }

    public void SimpleWalkingTransforms()
    {
        PlayerCameraBehaviour.FieldOfView(60);
        animator.SetLookAtWeight(1, 0.4f, 0.4f);
        animator.SetLookAtPosition(bodyData.bodyAimPivot.position);
    }

    public void Fire()
    {
        weaponHolder.gun.Fire();
    }
}
