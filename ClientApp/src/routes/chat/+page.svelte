<script lang="ts">
    import { onMount } from 'svelte';
    import ChatConversationList from '$lib/components/chat/ChatConversationList.svelte';
    import ChatWindow from '$lib/components/chat/ChatWindow.svelte';
    import {
        fetchConversations,
        currentConversation,
        clearCurrentConversation,
        chatError
    } from '$lib/stores/chatStore';

    let sidebarOpen = true;
    let isMobile = false;

    function toggleSidebar() {
        sidebarOpen = !sidebarOpen;
    }

    function closeSidebar() {
        sidebarOpen = false;
    }

    // Close sidebar when conversation is selected on mobile
    $: if ($currentConversation && isMobile) {
        closeSidebar();
    }

    function checkMobile() {
        isMobile = window.innerWidth < 768;
        // Default sidebar closed on mobile, open on desktop
        if (isMobile && sidebarOpen) {
            sidebarOpen = false;
        }
    }

    onMount(() => {
        fetchConversations();
        checkMobile();
        window.addEventListener('resize', checkMobile);

        return () => {
            window.removeEventListener('resize', checkMobile);
            clearCurrentConversation();
        };
    });
</script>

<svelte:head>
    <title>Chat - Lionheart</title>
</svelte:head>

<div class="h-[calc(100vh-4rem)] bg-base-200">
    <div class="h-full max-w-7xl mx-auto flex relative">
        <!-- Mobile Backdrop -->
        {#if sidebarOpen && isMobile}
            <button
                on:click={closeSidebar}
                class="md:hidden fixed inset-0 z-30 bg-black/50 backdrop-blur-sm"
                aria-label="Close sidebar"
            ></button>
        {/if}

        <!-- Sidebar - Conversation List -->
        <aside
            class="
                {isMobile
                    ? `fixed inset-y-0 left-0 z-40 w-72 transform transition-transform duration-200 ease-out
                       ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'}`
                    : `relative flex-shrink-0 transition-all duration-200 ease-out
                       ${sidebarOpen ? 'w-72' : 'w-0 overflow-hidden'}`}
                bg-base-100 border-r border-base-content/10
            "
            style={isMobile ? 'top: 4rem;' : ''}
        >
            <div class="w-72 h-full">
                <ChatConversationList />
            </div>
        </aside>

        <!-- Main Chat Area -->
        <main class="flex-1 min-w-0 flex flex-col">
            <ChatWindow {sidebarOpen} on:toggleSidebar={toggleSidebar} />
        </main>
    </div>

    <!-- Error Toast -->
    {#if $chatError}
        <div class="fixed bottom-4 right-4 z-50">
            <div class="alert alert-error shadow-lg max-w-sm">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-5 h-5">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 1 1-18 0 9 9 0 0 1 18 0Zm-9 3.75h.008v.008H12v-.008Z" />
                </svg>
                <span class="text-sm">{$chatError}</span>
                <button
                    on:click={() => chatError.set(null)}
                    class="btn btn-ghost btn-xs btn-circle"
                >
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor" class="w-4 h-4">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                    </svg>
                </button>
            </div>
        </div>
    {/if}
</div>
