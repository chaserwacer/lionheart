<script lang="ts">
    import type { Activity } from "./activityStore";

    export let activity: Activity;
    let date = activity.dateTime.toWellFormed();
</script>

<div class="card border border-primary">
    <div class="card-body m-2.5 p-2.5">
        <div class="flex flex-row">
            <div class="w-150">
                <h1 class="card-title">{activity.name}</h1>
                <p>{date}</p>
                <p>Length: {activity.timeInMinutes} minutes</p>
                <div class="divider m-0"></div>
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
            {#if activity.liftDetails != null || activity.runWalkDetails != null || activity.rideDetails != null}
                <div class="divider divider-horizontal"></div>
            {/if}

            <div class="w-150">
                {#if activity.liftDetails != null}
                    <h1 class="card-title">Lift Specific Details</h1>
                    <div class="divider m-0"></div>
                    <p class="font-bold">Tonnage / Total Volume</p>
                    {activity.liftDetails.tonnage}
                    <p class="mt-1 font-bold">Lift Focus</p>
                    {activity.liftDetails.liftFocus}
                    <p class="mt-1 font-bold">Lift Type</p>
                    {activity.liftDetails.liftType}
                    <div class="divider m-0"></div>
                    <p class="font-bold">Summary</p>
                    {activity.userSummary}
                {:else if activity.rideDetails != null}
                    <h1 class="card-title">Ride Specific Details</h1>
                    <div class="divider m-0"></div>
                {:else if activity.runWalkDetails != null}
                    <h1 class="card-title">Run/Walk Specific Details</h1>
                    <div class="divider m-0"></div>
                {/if}
            </div>
        </div>
    </div>
</div>
