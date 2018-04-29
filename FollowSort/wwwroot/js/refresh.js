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

        const promises = [];

        for (let a of artists) {
            const url = a.sourceSite === "Twitter" ? `/api/twitter/refresh/${a.id}`
                : a.sourceSite === "Tumblr" ? `/api/tumblr/refresh/${a.id}`
                    : null;
            if (url !== null) {
                const name = $("<li></li>")
                    .text(a.name)
                    .appendTo(ul);
                const p = fetch(url, {
                    method: "POST",
                    credentials: "same-origin"
                });
                p.then(() => name.css("color", "green")).catch(() => name.css("color", "red"));
                promises.push(p);
            }
        }

        try {
            await Promise.all(promises);
            location.href = location.href;
        } catch (e) {
            console.error(e);
            $("<p></p>")
                .text(e.message || "Could not load recent posts of all users.")
                .appendTo("#notifications");
        }
    });
});