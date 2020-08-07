import PropTypes from 'prop-types';
import React, { Component, Fragment } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSeparator from 'Components/Page/Toolbar/PageToolbarSeparator';
import { icons } from 'Helpers/Props';
import SettingsToolbarConnector from 'Settings/SettingsToolbarConnector';
import ImportExclusionsConnector from './ImportExclusions/ImportExclusionsConnector';
import ImportListsConnector from './ImportLists/ImportListsConnector';
import ImportListOptionsConnector from './Options/ImportListOptionsConnector';

class ImportListSettings extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this._saveCallback = null;

    this.state = {
      isSaving: false,
      hasPendingChanges: false
    };
  }

  //
  // Listeners

  onChildMounted = (saveCallback) => {
    this._saveCallback = saveCallback;
  }

  onChildStateChange = (payload) => {
    this.setState(payload);
  }

  onSavePress = () => {
    if (this._saveCallback) {
      this._saveCallback();
    }
  }

  // Render
  //

  render() {
    const {
      isTestingAll,
      dispatchTestAllImportList
    } = this.props;

    const {
      isSaving,
      hasPendingChanges
    } = this.state;

    return (
      <PageContent title="List Settings">
        <SettingsToolbarConnector
          isSaving={isSaving}
          hasPendingChanges={hasPendingChanges}
          additionalButtons={
            <Fragment>
              <PageToolbarSeparator />

              <PageToolbarButton
                label="Test All Lists"
                iconName={icons.TEST}
                isSpinning={isTestingAll}
                onPress={dispatchTestAllImportList}
              />
            </Fragment>
          }
          onSavePress={this.onSavePress}
        />

        <PageContentBody>
          <ImportListsConnector />

          <ImportListOptionsConnector
            onChildMounted={this.onChildMounted}
            onChildStateChange={this.onChildStateChange}
          />

          <ImportExclusionsConnector />

        </PageContentBody>
      </PageContent>
    );
  }
}

ImportListSettings.propTypes = {
  isTestingAll: PropTypes.bool.isRequired,
  dispatchTestAllImportList: PropTypes.func.isRequired
};

export default ImportListSettings;
