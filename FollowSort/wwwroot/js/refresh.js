window.addEventListener("load", () => {
    $("#refreshButton").click(async e => {
        e.preventDefault();

        const ul = $("<ul></ul>");

        $("#notifications")
            .empty()
            .append("<h2>Refreshing notifications...</h2>")
            .append(ul);

        const artists = await fetch("/api/artists", {
            credentials: "same-origin"
        }).then(r => r.json());

        const tasks = [];
        for (let a of artists) {
            const url = `/api/artists/${a.id}/refresh`;
            if (url !== null) {
                const x = $("<li></li>")
                    .text(`${a.name} (${a.sourceSite})`)
                    .appendTo(ul);
                tasks.unshift({
                    element: x,
                    url: url
                });
            }
        }

        await $.getScript("/js/es6-promise-pool.js");
        
        var producer = () => {
            const task = tasks.pop();
            if (!task) return null;

            const promise = (async () => {
                const r = await fetch(task.url, {
                    method: "POST",
                    credentials: "same-origin"
                });
                if (!r.ok) throw new Error(`Request failed with status code ${r.ok}`);
            })();
            promise
                .then(() => task.element.css("color", "green"))
                .catch(() => task.element.css("color", "red"));
            return promise;
        };
        
        try {
            await new PromisePool(producer, 4).start();
            location.href = location.href;
        } catch (e) {
            console.error(e);
            $("<p></p>")
                .text("Could not load recent posts of all users. Please try again later.")
                .appendTo("#notifications");
            $("<a></a>")
                .text("Return to notifications")
                .attr("href", location.href)
                .appendTo("#notifications");
        }
    });
});