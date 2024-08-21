export async function syncOuraData(fetch: { (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (input: RequestInfo | URL, init?: RequestInit): Promise<Response>; (arg0: string): any; }) {
    try {
        const url =
            "api/Oura/SyncOuraData";
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
            },
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