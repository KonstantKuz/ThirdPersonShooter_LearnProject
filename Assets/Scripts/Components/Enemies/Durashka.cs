﻿using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using Pathfinding;
using StateMachine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;


public class Durashka : Enemy, IDamageable
{
    [Header("Movement")]
    [SerializeField] private float movementSpeed;
    [SerializeField] private float jumpSpeed;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController controller;
    [SerializeField] private RigBuilder rig;
    
    [Header("Targeting")]
    [SerializeField] private Gun gun;
    [SerializeField] private float targetingSpeed;
    [SerializeField] private TargetInterpolator targetInterpolator;
    [SerializeField] private LookAtConstraint[] targetingConstraints;
    
    private Transform currentAttackTarget;

    private Vector3 movementVelocity;
    private Vector3 verticalVelocity;
    private Vector3 dashVelocity;
    
    public int TotalHealth { get; private set; }

    public override void Start()
    {
        base.Start();
        TotalHealth = Constants.TotalHealth.Durashka;
        gun.SetDamageValue(Constants.DamagePerHit.Durashka);
        StartAiming();
        GoToRandomPlayerSidePeriodically();
        
        GameBeatSequencer.OnBPM += TryFire;
    }

    private void TryFire()
    {
        if (Random.value > 0.5f)
        {
            gun.Fire();
        }
    }

    private void GoToRandomPlayerSidePeriodically()
    {
        StartCoroutine(GoToRandomSide());
        IEnumerator GoToRandomSide()
        {
            while (true)
            {
                GoToRandomPlayerSide();
                yield return new WaitForSeconds(pathUpdPeriod);
            }
        }
    }
    
    private void GoToRandomPlayerSide()
    {
        Vector3 side = player.transform.right * RandomSign() + player.transform.forward * RandomSign();
        Vector3 pointToGo = player.transform.position + side * Random.Range(5,8);
        
        // NavGraph generalGraph = 
        //     AstarPath.active.data.FindGraph(graphToFind => graphToFind.name == Constants.GeneralGraph);
        //
        // Vector3 resultNode = (Vector3)generalGraph.GetNearest(pointToGo).node.position;

        UpdatePath(pointToGo);
    }

    private int RandomSign()
    {
        return Random.value > 0.5f ? 1 : -1;
    }

    private void StartAiming()
    {
        animator.SetBool(AnimatorHashes.AimingHash, true);
        currentAttackTarget = player.Animator.GetBoneTransform(HumanBodyBones.Spine);
        targetInterpolator.SetConstraint(currentAttackTarget, targetingSpeed);
        for (int i = 0; i < targetingConstraints.Length; i++)
        {
            targetingConstraints[i].AddSource(Interpolator());
        }
    }

    private ConstraintSource Interpolator()
    {
        ConstraintSource interpolatorSource = new ConstraintSource();
        interpolatorSource.weight = 1;
        interpolatorSource.sourceTransform = targetInterpolator.transform;
        return interpolatorSource;
    }
    
    public override void CustomUpdate()
    {
        if (player == null)
            return;
        
        ApplyGravity();

        RotateTowards(player.transform.position);

        CleanPassedNodes();
        if (!HasPath() || PathFullyPassed())
        {
            verticalVelocity.y = 0;
            Move(0,0);
            return;
        }
        
        CalculateMoveValuesToNextNode();
        MoveToNextNode();
    }

    private void ApplyGravity()
    {
        verticalVelocity += Physics.gravity * Time.deltaTime;
        verticalVelocity.y = Mathf.Clamp(verticalVelocity.y, Physics.gravity.y, jumpSpeed);
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    public override void MoveToNextNode()
    {
        Move(verticalMoveValue, horizontalMoveValue);
    }

    public void Move(float verticalValue, float horizontalValue)
    {
        float interpolatedVertical =
            Mathf.Lerp(animator.GetFloat(AnimatorHashes.VerticalHash), verticalValue, Time.deltaTime * 5f);
        
        float interpolatedHorizontal =
            Mathf.Lerp(animator.GetFloat(AnimatorHashes.HorizontalHash), horizontalValue, Time.deltaTime * 5f);
        
        animator.SetFloat(AnimatorHashes.VerticalHash, interpolatedVertical);
        animator.SetFloat(AnimatorHashes.HorizontalHash, interpolatedHorizontal);
        
        movementVelocity = Vector3.zero;

        movementVelocity.x = interpolatedHorizontal;
        movementVelocity.z = interpolatedVertical;
        
        movementVelocity = transform.TransformDirection(movementVelocity);
        movementVelocity *= movementSpeed;

        controller.Move(movementVelocity * Time.deltaTime);
    }
    
    public void TakeDamage(int value)
    {
        TotalHealth -= value;
        if (TotalHealth > 0)
        {
            return;
        }
        
        animator.SetTrigger(AnimatorHashes.DeathHash);
        animator.SetFloat(AnimatorHashes.DeathTypeHash, Random.Range(0, 4));
        
        StopAllCoroutines();
        DisableGun();
        DisableActivities();
        DelayedSpawnExplosion();
    }

    private void DelayedSpawnExplosion()
    {
        StartCoroutine(DelayedSpawnExplosion());
        IEnumerator DelayedSpawnExplosion()
        {
            yield return new WaitForSeconds(2f);
            ObjectPooler.Instance.SpawnObject(Constants.PoolExplosionMid, transform.position);
            ObjectPooler.Instance.ReturnObject(gameObject, gameObject.name);
        }
    }

    private void DisableGun()
    {
        GameBeatSequencer.OnBPM -= TryFire;
        gun.GetComponent<Rigidbody>().isKinematic = false;
    }

    private void DisableActivities()
    {
        rig.enabled = false;
        controller.enabled = false;
        enabled = false;
    }
}
