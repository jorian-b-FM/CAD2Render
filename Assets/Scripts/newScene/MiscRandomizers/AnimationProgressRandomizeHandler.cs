using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationProgressRandomizeHandler : RandomizerInterface
{
    private Animator animator;
    
    public override MainRandomizerData.RandomizerTypes randomizerType => MainRandomizerData.RandomizerTypes.Object;

    public override void Randomize(ref RandomNumberGenerator rng, BOPDatasetExporter.SceneIterator bopSceneIterator = null)
    {
        if(animator == null)
            animator = GetComponent<Animator>();
        animator.Play(0, 0, rng.Next());
        animator.speed = 0f;
        resetFrameAccumulation();
    }

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
    }
}
