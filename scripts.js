function slugify(text) {
    text = text.toString().toLowerCase().trim();

    const sets = [
    {to: 'a', from: '[ÀÁÂÃÄÅÆĀĂĄẠẢẤẦẨẪẬẮẰẲẴẶ]'},
    {to: 'c', from: '[ÇĆĈČ]'},
    {to: 'd', from: '[ÐĎĐÞ]'},
    {to: 'e', from: '[ÈÉÊËĒĔĖĘĚẸẺẼẾỀỂỄỆ]'},
    {to: 'g', from: '[ĜĞĢǴ]'},
    {to: 'h', from: '[ĤḦ]'},
    {to: 'i', from: '[ÌÍÎÏĨĪĮİỈỊ]'},
    {to: 'j', from: '[Ĵ]'},
    {to: 'ij', from: '[Ĳ]'},
    {to: 'k', from: '[Ķ]'},
    {to: 'l', from: '[ĹĻĽŁ]'},
    {to: 'm', from: '[Ḿ]'},
    {to: 'n', from: '[ÑŃŅŇ]'},
    {to: 'o', from: '[ÒÓÔÕÖØŌŎŐỌỎỐỒỔỖỘỚỜỞỠỢǪǬƠ]'},
    {to: 'oe', from: '[Œ]'},
    {to: 'p', from: '[ṕ]'},
    {to: 'r', from: '[ŔŖŘ]'},
    {to: 's', from: '[ßŚŜŞŠ]'},
    {to: 't', from: '[ŢŤ]'},
    {to: 'u', from: '[ÙÚÛÜŨŪŬŮŰŲỤỦỨỪỬỮỰƯ]'},
    {to: 'w', from: '[ẂŴẀẄ]'},
    {to: 'x', from: '[ẍ]'},
    {to: 'y', from: '[ÝŶŸỲỴỶỸ]'},
    {to: 'z', from: '[ŹŻŽ]'},
    {to: '-', from: '[·/_,:;\']'}
    ];

    sets.forEach(set => {
    text = text.replace(new RegExp(set.from,'gi'), set.to)
    });

    return text.toString().toLowerCase()
    .replace(/\s+/g, '-')    
    .replace(/&/g, '-and-')  
    .replace(/[^\w\-]+/g, '')
    .replace(/\--+/g, '-')   
    .replace(/^-+/, '')      
    .replace(/-+$/, '');     
}

function isURL(url) {
    return /^(https?|s?ftp):\/\/(((([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-f]{2})|[!\$&'\(\)\*\+,;=]|:)*@)?(((\d|[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.(\d|[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.(\d|[1-9]\d|1\d\d|2[0-4]\d|25[0-5])\.(\d|[1-9]\d|1\d\d|2[0-4]\d|25[0-5]))|((([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.?)(:\d*)?)(\/((([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-f]{2})|[!\$&'\(\)\*\+,;=]|:|@)+(\/(([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-f]{2})|[!\$&'\(\)\*\+,;=]|:|@)*)*)?)?(\?((([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-f]{2})|[!\$&'\(\)\*\+,;=]|:|@)|[\uE000-\uF8FF]|\/|\?)*)?(#((([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(%[\da-f]{2})|[!\$&'\(\)\*\+,;=]|:|@)|\/|\?)*)?$/i.test(url);
}

function log(text){
    console.log(text);
    $("#markdown").append(text+" ...<br/>");
}

function getPage() {
    var url = $(location).attr('href');
    var pageRegex = /\?(.*?)(#|$)/;
    var pageMatches = pageRegex.exec(url);
    if (pageMatches == null || pageMatches.length < 2) return null;
    return pageMatches[1];
}

log("Parsing repository information from URL")
var username = location.hostname.split(".")[0];
var repoRegex = /github\.io\/(.*?)\//;
var repoName = repoRegex.exec($(location).attr('href'))[1];
$("#repo-link").attr("href", "https://github.com/"+username+"/"+repoName);
log("Parsed username: "+username+" and repository: "+repoName);

function getRawFileUrl(fileName){
    return "https://raw.githubusercontent.com/"+username+"/"+repoName+"/master/"+(fileName.replace(/^\/+/,''));
}

function getConfigUrl(){
    return getRawFileUrl("markdown-pages.txt");
}

function formatLink(page){
    var baseUrl = "https://"+username+".github.io/"+repoName+"/";
    if (!page) return baseUrl;
    var slugified = slugify(page);
    return baseUrl+"?"+slugified;
}

var TocAnchorMap = {}

function setPageMarkdown(md) {
    log("Converting Markdown")

    var linkRegex = /\[([^\[\]]+)\]\(([^)]+)/gm
    var match;
    while ((match = linkRegex.exec(md)) != null) {
        if (match.length > 2 && match[2][0] == "#")
            TocAnchorMap[slugify(match[1])] = match[2].slice(1);
    }

    var converter = new showdown.Converter({noHeaderId: true});
    var md_html = converter.makeHtml(md);
    $("#markdown").html(md_html);
    $(":header").prepend(function (){
        var id = slugify($(this).text());
        if (id in TocAnchorMap) id = TocAnchorMap[id];
        return `
        <a class="anchor" id="`+ id +`" href="#`+ id +`" aria-hidden="true">
        <svg class="octicon-link" viewBox="0 0 16 16" version="1.1" width="16" height="16" aria-hidden="true">
        <path fill-rule="evenodd" d="M4 9h1v1H4c-1.5 0-3-1.69-3-3.5S2.55 3 4 3h4c1.45 0 3 1.69 3 3.5 0 1.41-.91 2.72-2 3.25V8.59c.58-.45 1-1.27 1-2.09C10 5.22 8.98 4 8 4H4c-.98 0-2 1.22-2 2.5S3 9 4 9zm9-3h-1v1h1c1 0 2 1.22 2 2.5S13.98 12 13 12H9c-.98 0-2-1.22-2-2.5 0-.83.42-1.64 1-2.09V6.25c-1.09.53-2 1.84-2 3.25C6 11.31 7.55 13 9 13h4c1.45 0 3-1.69 3-3.5S14.5 6 13 6z">
        </path>
        </svg>
        </a>
        `;
    });
    if (location.hash) location.href = location.hash;
}

var confURL = getConfigUrl();
log("Loading JustTheMD configuration from "+confURL);

var confPages = {}
var page = getPage();

var confRequest = $.get(confURL);

confRequest.done(function (confFile) {

    log("Configuration file found!");

    lines = confFile.split("\n");

    var footNote = lines[0];

    $("#footnote").html(footNote);

    for (var i = 1; i < lines.length; i++) {
        line = lines[i];
        if (!line) continue;
        split = line.split("=");
        var pageName = split[0].trim();
        var fileURL = split[1].trim();
        if (!isURL(fileURL)) fileURL = getRawFileUrl(fileURL);
        var pageSlug = slugify(pageName);
        if (page == null) page = pageSlug;
        if (pageSlug == page) document.title = pageName;
        confPages[pageSlug] = fileURL;
        $("#pages-nav-list").append('<li><a href="'+formatLink(i == 1 ? null : pageName)+'"><span>'+pageName+'</span></a></li>')
    }

    if (!page) log("ERROR: No configured page found!")

    log("Configured pages: "+Object.keys(confPages).length);

    var pageURL = confPages[page];

    if (!pageURL) log("ERROR: Can't load configuration for page: " + page);

    log("Loading page: '"+page+"' from: "+pageURL);

    $.get(pageURL)
        .done(function (md) { setPageMarkdown(md); })
        .fail(function() { log("ERROR: Couldn't load page markdown"); });
});

confRequest.fail(function() {
    log("No configuration file found, using README.md");
    $.get(getRawFileUrl("README.md"))
        .done(function (md) {
            setPageMarkdown(md);
            document.title = repoName + " Readme";
            $("#pages-nav-list").append('<li><span>'+repoName+'</span></li>');
            $("#footnote").html('Automatically generated by <a href="https://github.com/Alan-FGR/JustTheMD">JustTheMD</a>');
        })
        .fail(function() {
            log(`ERROR: Couldn\'t load README.md. 
            <h3><a href="https://github.com/Alan-FGR/JustTheMD">Help Configuring JustTheMD</a></h3>`); });
});