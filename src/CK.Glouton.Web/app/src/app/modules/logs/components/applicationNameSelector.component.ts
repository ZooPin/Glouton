import { Component, OnInit } from '@angular/core';
import { Store } from '@ngrx/store';
import { Observable } from 'rxjs/Observable';
import { Subscription } from 'rxjs/Subscription';
import { EffectDispatcher } from '@ck/rx';
import { IAppState } from 'app/app.state';
import { LogService } from 'app/_services';
import { ILogViewModel } from 'app/common/logs/models';
import { SubmitAppNamesEffect } from '../actions';
import { MessageService } from 'primeng/components/common/messageservice';

@Component({
    selector: 'applicationNameSelector',
    templateUrl: 'applicationNameSelector.component.html'
})

export class ApplicationNameSelectorComponent implements OnInit {

    private _applicationNames$: Observable<string[]>;
    private _applicationNames: string[];

    private _selected: string[];
    private _subscription: Subscription;

    constructor(
        private logService: LogService,
        private effectDispatcher: EffectDispatcher,
        private store: Store<IAppState>,
        private messageService: MessageService
    ) {
        this._applicationNames$ = this.store.select(s => s.logsParameters.appNames);
        this._subscription = this._applicationNames$.subscribe(a => this._selected = a);
    }

    onChange(_: boolean): void {
        this.effectDispatcher.dispatch(new SubmitAppNamesEffect(this._selected));
    }

    ngOnInit(): void {
        this.logService.getAllApplicationName()
            .subscribe(n => this._applicationNames = n,
            error => this.messageService.add({
                severity: 'error', summary: 'Error', detail: 'Error while trying to get the log'
            }));
    }
}