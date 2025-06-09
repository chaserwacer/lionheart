export async function syncOuraData(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        // Calculate endDate as today
        const today = new Date();
        const year = today.getFullYear();
        const month = String(today.getMonth() + 1).padStart(2, '0');
        const day = String(today.getDate()).padStart(2, '0');
        const endDate = `${year}-${month}-${day}`;

        // Calculate startDate as 7 days before endDate
        const start = new Date(today);
        start.setDate(start.getDate() - 7);
        const startYear = start.getFullYear();
        const startMonth = String(start.getMonth() + 1).padStart(2, '0');
        const startDay = String(start.getDate()).padStart(2, '0');
        const startDate = `${startYear}-${startMonth}-${startDay}`;

        const url = "api/oura/sync";
        const body = {
            startDate,
            endDate
        };
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
            },
            body: JSON.stringify(body)
        });

        if (response.ok) {
            return true;
        } else {
            console.error("Failed to sync oura");
        }
    } catch {
        console.error("Error syncing oura");
    }
}

export async function GetDailyOuraInfo(date: string, fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        // New endpoint: GET to /api/oura/get-daily-oura-data?request=YYYY-MM-DD
        const url = "api/oura/get-daily-oura-data?request=" + date;
        const response = await fetch(url);

        if (response.ok) {
            const data = await response.json();
            return data;
        } else {
            console.error("Failed to get daily oura data");
        }
    } catch {
        console.error("Error fetching daily oura data");
    }

}

export type DailyOuraInfo = {
    objectID: '',
    date: '', // Assuming DateOnly is a string representation
    resilienceData: {
        sleepRecovery: 0,
        daytimeRecovery: 0,
        stress: 0,
        resilienceLevel: 0,
    },
    activityData: {
        activityScore: 0,
        steps:0,
        activeCalories: 0,
        totalCalories: 0,
        targetCalories: 0,
        meetDailyTargets: 0,
        moveEveryHour: 0,
        recoveryTime: 0,
        stayActive: 0,
        trainingFrequency: 0,
        trainingVolume: 0,
    },
    sleepData: {
        sleepScore: 0,
        deepSleep: 0,
        efficiency: 0,
        latency: 0,
        remSleep: 0,
        restfulness: 0,
        timing: 0,
        totalSleep: 0,
    },
    readinessData: {
        readinessScore: 0,
        temperatureDeviation:0,
        activityBalance: 0,
        bodyTemperature: 0,
        hrvBalance: 0,
        previousDayActivity: 0,
        previousNight: 0,
        recoveryIndex: 0,
        restingHeartRate: 0,
        sleepBalance: 0,
    }
}
