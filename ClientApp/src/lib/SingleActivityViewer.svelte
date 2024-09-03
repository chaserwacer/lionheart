<script lang="ts">
    import type { Activity } from "./activityStore";

    export let activity: Activity;
    let date = activity.dateTime.toWellFormed();
</script>

<div class="card ">
    <div class="card-body m-2.5 p-2.5">
        <div class="flex flex-row">
            <div class="w-52">
                <h1 class="card-title">{activity.name}</h1>
                <p>{date}</p>
                <p>Length: {activity.timeInMinutes} minutes</p>
                <div class="divider divider-primary m-0"></div>
                <!-- <p class="font-bold">Summary</p>
                {activity.userSummary}
                <div class="divider m-0"></div> -->

                <p class="font-bold">Calories Burned:</p>
                {activity.caloriesBurned}

                <p class="font-bold">Difficulty Rating:</p>
                {activity.difficultyRating} / 5

                <p class="font-bold">Accumulated Fatigue:</p>
                {activity.accumulatedFatigue} / 5

                <p class="font-bold">External Variables Rating:</p>
                {activity.externalVariablesRating} / 5

                <p class="font-bold">Engagement Rating:</p>
                {activity.engagementRating} / 5
            </div>
            <!-- {#if activity.liftDetails != null || activity.runWalkDetails != null || activity.rideDetails != null} -->
                <div class="divider divider-primary divider-horizontal"></div>
            <!-- {/if} -->

            <div class="w-60">
                {#if activity.liftDetails != null}
                    <h1 class="card-title">Lift Specific Details</h1>
                    <div class="divider divider-primary m-0"></div>
                    <p class="font-bold">Tonnage / Total Volume</p>
                    {activity.liftDetails.tonnage}
                    <p class="mt-1 font-bold">Lift Focus</p>
                    {activity.liftDetails.liftFocus}
                    <p class="mt-1 font-bold">Lift Type</p>
                    {activity.liftDetails.liftType}
                    <div class="divider divider-primary m-0"></div>
                    <p class="font-bold">Summary</p>
                    {activity.userSummary}
                    
                {:else if activity.rideDetails != null}
                    <h1 class="card-title">Ride Specific Details</h1>
                    <div class="divider m-0"></div>
                    <p class="font-bold">Distance</p>
                    {activity.rideDetails.distance} miles
                    <p class="font-bold">Elevation Gain</p>
                    {activity.rideDetails.elevationGain} feet
                    <p class="font-bold">Average Power</p>
                    {activity.rideDetails.averagePower}
                    <p class="font-bold">Average Speed</p>
                    {activity.rideDetails.averageSpeed}
                    <p class="font-bold">Ride Type</p>
                    {activity.rideDetails.rideType}
                {:else if activity.runWalkDetails != null}
                    <h1 class="card-title">Run/Walk Specific Details</h1>
                    <div class="divider divider-primary m-0"></div>
                    <p class="font-bold">Distance</p>
                    {activity.runWalkDetails.distance} miles
                    <p class="font-bold">Elevation Gain</p>
                    {activity.runWalkDetails.elevationGain} feet
                    <p class="font-bold">Average Pace (in seconds)</p>
                    {activity.runWalkDetails.averagePaceInSeconds}
                    <p class="font-bold">Run Type</p>
                    {activity.runWalkDetails.runType}
                {:else}
                    <p class="font-bold">Summary</p>
                    {activity.userSummary}
                {/if}
            </div>
            
        </div>
        {#if activity.liftDetails != null}
            <div class="divider divider-primary">Set Tracker</div>
                <div class="flex flex-wrap">
                    {#if activity.liftDetails.quadSets != 0}
                    <p class="font-bold">Quads: {activity.liftDetails.quadSets}</p> 
                    {/if}
                    {#if activity.liftDetails.hamstringSets != 0}
                    <p class="font-bold">Hamstrings: {activity.liftDetails.hamstringSets}</p> 
                    {/if}
                    {#if activity.liftDetails.chestSets != 0}
                    <p class="font-bold">Chest: {activity.liftDetails.chestSets}</p> 
                    {/if}
                    {#if activity.liftDetails.backSets != 0}
                    <p class="font-bold">Back: {activity.liftDetails.backSets}</p> 
                    {/if}
                    {#if activity.liftDetails.shoulderSets != 0}
                    <p class="font-bold">Shoulders: {activity.liftDetails.shoulderSets}</p> 
                    {/if}
                    {#if activity.liftDetails.tricepSets != 0}
                    <p class="font-bold">Triceps: {activity.liftDetails.tricepSets}</p> 
                    {/if}
                    {#if activity.liftDetails.bicepSets != 0}
                    <p class="font-bold">Biceps: {activity.liftDetails.bicepSets}</p> 
                    {/if}
                </div>
            {/if}
    </div>
</div>
