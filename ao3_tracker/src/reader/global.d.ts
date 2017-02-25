declare namespace Ao3Track {
    export namespace Marshal
    {
        export type TypeConverter = (value:any)=>any;
        export type Type = TypeConverter;

        export interface IMemberDef
        {
            return?: Type;
            args?: {[key:number]: Type};
            getter?: Type|boolean;
            setter?: Type|boolean;
            promise?: number;
        }
        export interface IHelperDef
        {
            [key:string]: IMemberDef;
        }
    }
        
    export interface IPageTitle
    {
        title: string;
        chapter?: string | null;
        chaptername?: string | null;
        authors?: string[]  | null;
        fandoms?: string[] | null;
        primarytag?: string | null;
    }    
    
    export interface IAo3TrackHelper {
        getWorkChaptersAsync(works: number[], callback: (workchapters: { [key:number]:IWorkChapter }) => void) : void;

        setWorkChapters(workchapters: { [key: number]: IWorkChapter; }): void;

        onjumptolastlocationevent: ((pagejump: boolean) => void) | null;

        jumpToLastLocationEnabled: boolean;

        nextPage: string;
        prevPage: string;
        canGoBack: boolean;
        canGoForward: boolean;
        goBack(): void;
        goForward(): void;
        leftOffset: number;
        opacity: number;
        showPrevPageIndicator: number;
        showNextPageIndicator: number;

        onalterfontsizeevent: ((ev: any) => void) | null;
        fontSize: number;

        showContextMenu(x: number, y: number, menuItems: string[], callback: (selected: string | null)=>void): void;
        addToReadingList(href: string): void;
        copyToClipboard(str: string, type: string): void;
        setCookies(cookies: string): void;

        currentLocation: IWorkChapterEx | null;
        pageTitle : IPageTitle | null;

        areUrlsInReadingListAsync : (urls: string[], callback: (result: { [key:string]:boolean})=> void) => void;        
    }

    export var Helper : Ao3Track.IAo3TrackHelper;
}